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
        private readonly record struct SummonSourceCandidate(int SkillId, SkillData Skill, WzImageProperty SkillNode);
        private readonly record struct ItemBulletAnimationCacheKey(int ItemId, int WeaponItemId, int WeaponCode);
        private readonly record struct ClientSummonedUolCandidateValue(string Value, string[] ContextPathParts);
        private readonly record struct ClientSkillAssetUolPathResolution(
            string RootPath,
            SortedDictionary<int, string> CharacterLevelPaths,
            Dictionary<int, string> LevelPaths);

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

        public const int ClientAfterimageCanvasDelayFallbackMs = 120;
        private const int ClientSummonedFrameDelayFallbackMs = 120;
        private const int ClientSummonReversePlaybackStringPoolId = 0x049F;
        private const string ClientSummonReversePlaybackFallbackName = "zigzag";

        private static readonly string[] ClientSummonedUolPropertyNames =
        {
            "sSummonedUOL",
            "summonedUOL",
            "summonedUol",
            "summonUOL",
            "summonUol"
        };
        private static readonly string[] ClientSummonedUolTableWrapperNames =
        {
            "client",
            "clientData",
            "clientOwned",
            "clientExtracted",
            "clientExtraction",
            "clientSidecar",
            "hidden",
            "sidecar",
            "skillEntry",
            "SKILLENTRY",
            "skillInfo",
            "CSkillInfo",
            "tables",
            "table"
        };
        private static readonly string[] ClientSummonedUolTableOwnerFieldNames =
        {
            "skillid",
            "nskillid",
            "currentskillid",
            "sourceskillid",
            "currentskill",
            "sourceskill",
            "currentskillpath",
            "sourceskillpath",
            "summonedskillid",
            "summonedskill",
            "summonedskillpath",
            "skill",
            "nskill",
            "skillpath",
            "skillidpath",
            "id",
            "key",
            "owner",
            "ownerskill",
            "ownerid",
            "ownerpath",
            "ownerskillpath",
            "sourceowner",
            "summonowner",
            "summonedowner",
            "summonskillid",
            "ownerskillid"
        };
        private static readonly string[] ClientTileUolPropertyNames =
        {
            "sTileUOL",
            "tileUOL",
            "tileUol"
        };
        private static readonly string[] ClientBallUolPropertyNames =
        {
            "sBallUOL",
            "ballUOL",
            "ballUol"
        };
        private static readonly string[] ClientFlipBallUolPropertyNames =
        {
            "sFlipBallUOL",
            "flipBallUOL",
            "flipBallUol"
        };
        private const string ClientSummonedUolBranchName = "summon";
        private static readonly char[] ClientSummonedUolTokenTrimChars =
        {
            '"', '\'', '`', '(', ')', '[', ']', '{', '}', '<', '>', ':'
        };
        private static readonly string[] ClientSummonedUolEmbeddedSkillPrefixes =
        {
            "Skill/",
            "Skill.wz/",
            "wz/Skill/"
        };
        private const string ClientSummonedUolEmbeddedImagePathSuffix = ".img/";
        private const string ClientSummonedUolEmbeddedMountedSkillPathPrefix = "skill/";
        private const int ClientSummonedUolHeuristicInfoTraversalDepth = 4;
        private const int ClientSummonedUolHeuristicSkillTraversalDepth = 2;
        private const int ClientSummonedUolTableEntryTraversalDepth = 3;
        private const int ClientSummonedUolTableOwnerFieldTraversalDepth = 3;

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

        private static readonly IReadOnlyDictionary<int, int[]> ShadowPartnerRawActionSourceSkillIdsBySkillId =
            new Dictionary<int, int[]>
            {
                [4111002] = new[]
                {
                    4001003,
                    4101003, 4101004,
                    4111001, 4111003, 4111005, 4111007,
                    4121000, 4121003, 4121004, 4121008
                },
                [4211008] = new[]
                {
                    4001003,
                    4201002, 4201003, 4201004, 4201005,
                    4211001, 4211002, 4211003, 4211005, 4211006, 4211007,
                    4221000, 4221001, 4221003, 4221004, 4221006, 4221007
                },
                [14111000] = new[]
                {
                    14001003, 14001005,
                    14101002, 14101003, 14101006,
                    14111001, 14111002, 14111006
                }
            };

        private static readonly IReadOnlyDictionary<int, int[]> ClientFlagOnlyMorphTemplateOverridesBySkillId =
            new Dictionary<int, int[]>
            {
                // WZ-first evidence (`Skill/001.img/skill/0010109/info/morph = 1`) keeps a
                // current no-action, flag-only outlier in the morph metadata surface.
                // Keep this row explicit so future non-suffix remaps can stay in this seam
                // without widening suffix heuristics.
                [0010109] = new[] { 109 },
                // WZ-first evidence (`Skill/2002.img/skill/20020111/info/morph = 1`,
                // `action/0 = fastest`) keeps the checked action-backed flag-only outlier
                // on Morph/0111.img.
                [20020111] = new[] { 111 }
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
        private readonly Dictionary<int, int[]> _affectedSkillParentCache = new();
        private readonly Dictionary<int, int[]> _dummySkillParentCache = new();
        private readonly Dictionary<int, int[]> _requiredSkillParentCache = new();
        private readonly Dictionary<int, int[]> _passiveSkillParentCache = new();
        private readonly Dictionary<string, MeleeAfterImageCatalog> _characterAfterImageCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MeleeAfterImageCatalog> _characterChargeAfterImageCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingCharacterAfterImageKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingCharacterChargeAfterImageKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<ItemBulletAnimationCacheKey, SkillAnimation> _itemBulletAnimationCache = new();
        private readonly HashSet<ItemBulletAnimationCacheKey> _itemsWithoutBulletAnimation = new();
        private readonly Dictionary<SummonActionCacheKey, SkillAnimation> _summonActionCache = new();
        private readonly Dictionary<string, IReadOnlyList<ShadowPartnerClientActionResolver.ShadowPartnerActionPiece>> _shadowPartnerClientActionPieceCache =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingShadowPartnerClientActionPieceKeys = new(StringComparer.OrdinalIgnoreCase);
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
            {
                EnsureSkillPresentationLoaded(cached);
                return cached;
            }

            var skill = LoadSkillInternal(skillId, includePresentation: true);
            if (skill != null)
            {
                _skillCache[skillId] = skill;
            }
            return skill;
        }

        public SkillAnimation LoadItemBulletAnimation(int itemId, int weaponItemId = 0, int weaponCode = 0)
        {
            if (itemId <= 0)
            {
                return null;
            }

            var cacheKey = new ItemBulletAnimationCacheKey(itemId, weaponItemId, weaponCode);
            if (_itemBulletAnimationCache.TryGetValue(cacheKey, out SkillAnimation cached))
            {
                return cached;
            }

            if (_itemsWithoutBulletAnimation.Contains(cacheKey))
            {
                return null;
            }

            SkillAnimation animation = LoadItemBulletAnimationInternal(itemId, weaponItemId, weaponCode);
            if (animation?.Frames?.Count > 0)
            {
                _itemBulletAnimationCache[cacheKey] = animation;
                return animation;
            }

            _itemsWithoutBulletAnimation.Add(cacheKey);
            return null;
        }

        public SkillAnimation LoadSkillAnimationByNormalizedPath(string normalizedSkillAssetPath)
        {
            string normalizedPath = NormalizeClientSummonedUolPath(normalizedSkillAssetPath);
            if (!TryParseNormalizedSkillAnimationPath(normalizedPath, out string imageName, out string[] propertySegments))
            {
                return null;
            }

            WzImage skillImage = GetSkillImage(imageName);
            if (skillImage == null)
            {
                return null;
            }

            skillImage.ParseImage();

            WzImageProperty property = null;
            foreach (string segment in propertySegments)
            {
                property = property == null ? skillImage[segment] : property[segment];
                if (property == null)
                {
                    return null;
                }
            }

            SkillAnimation animation = LoadSkillAnimation(property, propertySegments[^1]);
            return animation.Frames.Count > 0 ? animation : null;
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

        private SkillData LoadSkillMetadata(int skillId)
        {
            if (_skillCache.TryGetValue(skillId, out SkillData cachedSkill))
            {
                return cachedSkill;
            }

            SkillData skill = LoadSkillInternal(skillId, includePresentation: false);
            if (skill != null)
            {
                _skillCache[skillId] = skill;
            }

            return skill;
        }

        private void EnsureSkillPresentationLoaded(SkillData skill)
        {
            if (skill == null || skill.PresentationDataLoaded)
            {
                return;
            }

            if (!TryGetSkillNode(skill.SkillId, out WzImageProperty skillNode))
            {
                return;
            }

            ParseSkillAnimations(skill, skillNode);
            LoadSkillIcon(skill, skillNode);
            skill.PresentationDataLoaded = true;
        }

        private SkillData LoadSkillInternal(int skillId, bool includePresentation)
        {
            if (!TryGetSkillNode(skillId, out WzImageProperty skillNode))
            {
                return null;
            }

            int jobId = skillId / 10000;

            var skill = new SkillData
            {
                SkillId = skillId,
                Job = jobId,
                PresentationDataLoaded = false
            };

            LoadSkillStrings(skill);

            // Parse basic info
            ParseSkillInfo(skill, skillNode);

            // Parse level data
            ParseSkillLevels(skill, skillNode);

            FinalizeMovementClassification(skill);

            if (includePresentation)
            {
                ParseSkillAnimations(skill, skillNode);
                LoadSkillIcon(skill, skillNode);
                skill.PresentationDataLoaded = true;
            }

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
            skill.ElementAttributeToken = ResolveSkillElementAttributeToken(skillNode);
            skill.Element = ResolvePrimarySkillElement(skill.ElementAttributeToken);

            // Basic properties from info node
            var infoNode = skillNode["info"];
            skill.Invisible = ResolveClientInvisibleSkillFlag(skillNode, infoNode);
            if (infoNode != null)
            {
                // Hidden skill
                skill.MasterOnly = GetInt(infoNode, "masterOnly") == 1;
                skill.IsRapidAttack = GetInt(infoNode, "rapidAttack") == 1;
                skill.IsMesoExplosion = GetInt(infoNode, "mesoExplosion") == 1;
                skill.IsMovingAttack = GetInt(infoNode, "movingAttack") == 1;
                skill.CasterMove = GetInt(infoNode, "casterMove") == 1;
                skill.AreaAttack = GetInt(infoNode, "areaAttack") == 1;
                skill.PullTarget = GetInt(infoNode, "pullTarget") == 1;
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
                skill.AffectedSkillIds = ParseLinkedSkillIds(infoNode["affectedSkill"]);
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

            IReadOnlyList<string> clientSummonedUolPaths = ResolveClientSummonedUolPaths(skillNode, infoNode);
            skill.ClientSummonedUolCandidatePaths = clientSummonedUolPaths.ToList();
            skill.ClientSummonedUolPath = clientSummonedUolPaths.FirstOrDefault();
            ClientSkillAssetUolPathResolution tileUolPathResolution = ResolveClientTileUolPathResolution(skillNode, infoNode);
            skill.ClientTileUolPath = tileUolPathResolution.RootPath;
            skill.ClientCharacterLevelTileUolPaths = tileUolPathResolution.CharacterLevelPaths;
            skill.ClientLevelTileUolPaths = tileUolPathResolution.LevelPaths;
            ClientSkillAssetUolPathResolution ballUolPathResolution = ResolveClientBallUolPathResolution(
                skillNode,
                infoNode,
                flip: false);
            skill.ClientBallUolPath = ballUolPathResolution.RootPath;
            skill.ClientCharacterLevelBallUolPaths = ballUolPathResolution.CharacterLevelPaths;
            skill.ClientLevelBallUolPaths = ballUolPathResolution.LevelPaths;
            ClientSkillAssetUolPathResolution flipBallUolPathResolution = ResolveClientBallUolPathResolution(
                skillNode,
                infoNode,
                flip: true);
            skill.ClientFlipBallUolPath = flipBallUolPathResolution.RootPath;
            skill.ClientCharacterLevelFlipBallUolPaths = flipBallUolPathResolution.CharacterLevelPaths;
            skill.ClientLevelFlipBallUolPaths = flipBallUolPathResolution.LevelPaths;

            if (string.IsNullOrWhiteSpace(skill.ElementAttributeToken))
            {
                skill.ElementAttributeToken = ResolveSkillElementAttributeToken(infoNode);
                skill.Element = ResolvePrimarySkillElement(skill.ElementAttributeToken);
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

            IReadOnlyList<string> morphRequestedActionNames = GetMorphTemplateRequestedActionNames(
                skillNode,
                skill.ActionNames);
            skill.MorphId = ResolveMorphTemplateId(
                skill.SkillId,
                commonNode,
                pvpCommonNode,
                infoNode,
                morphRequestedActionNames);

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

        private static string ResolveSkillElementAttributeToken(WzImageProperty node)
        {
            return node == null
                ? string.Empty
                : (GetString(node, "elemAttr") ?? string.Empty).Trim();
        }

        private static SkillElement ResolvePrimarySkillElement(string elementAttributeToken)
        {
            if (string.IsNullOrWhiteSpace(elementAttributeToken))
            {
                return SkillElement.Physical;
            }

            foreach (char token in elementAttributeToken.Trim())
            {
                switch (char.ToLowerInvariant(token))
                {
                    case 'f':
                        return SkillElement.Fire;
                    case 'i':
                        return SkillElement.Ice;
                    case 'l':
                        return SkillElement.Lightning;
                    case 's':
                        return SkillElement.Poison;
                    case 'h':
                        return SkillElement.Holy;
                    case 'd':
                        return SkillElement.Dark;
                    case 'p':
                        return SkillElement.Physical;
                }
            }

            return SkillElement.Physical;
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

            if (skill.UsesHelperOnlyAffectedSkillPassiveData)
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

        private static bool ResolveClientInvisibleSkillFlag(WzImageProperty skillNode, WzImageProperty infoNode)
        {
            return GetInt(infoNode, "invisible") == 1
                   || GetInt(skillNode, "invisible") == 1;
        }

        internal static bool ResolveClientInvisibleSkillFlagForTesting(WzImageProperty skillNode, WzImageProperty infoNode)
        {
            return ResolveClientInvisibleSkillFlag(skillNode, infoNode);
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

            var pvpCommonNode = skillNode["PVPcommon"];
            if (pvpCommonNode != null)
            {
                int maxLevel = Math.Max(1, GetInt(pvpCommonNode, "maxLevel", skill.MaxLevel > 0 ? skill.MaxLevel : 1));
                skill.MaxLevel = Math.Max(skill.MaxLevel, maxLevel);

                for (int level = 1; level <= maxLevel; level++)
                {
                    var levelData = CreateLevelData(skill, pvpCommonNode, level);
                    if (eventTamingMobId > 0 && levelData.ItemConNo <= 0)
                    {
                        levelData.ItemConNo = eventTamingMobId;
                    }

                    skill.PvpLevels[level] = levelData;
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
                skill.TargetHitEffects = LoadIndexedSkillAnimations(hitNode, "hit");
            }
            PopulateTargetHitCharacterLevelVariants(skill, skillNode["CharLevel"]);
            PopulateTargetHitLevelVariants(skill, skillNode["level"]);

            skill.MultipleLayerTargetHitEffects = LoadPrefixedIndexedSkillAnimations(skillNode, "hit");

            var mobNode = skillNode["mob"];
            if (mobNode != null)
            {
                skill.MobAnimation = LoadSkillAnimation(mobNode, "mob");
                skill.MobAnimationPath = NormalizeSkillAssetPath(mobNode);
            }

            // Load affected (buff visual)
            var affectedNode = skillNode["affected"];
            if (affectedNode != null)
            {
                skill.AffectedEffect = LoadSkillAnimation(affectedNode, "affected");
            }

            skill.AffectedSecondaryEffect = LoadOptionalSkillAnimation(skillNode, "affected0", loop: false);
            skill.SpecialAffectedEffect = LoadOptionalSkillAnimation(skillNode, "specialAffected", loop: false);

            LoadPersistentAvatarEffects(skill, skillNode);
            LoadShadowPartnerActionAnimations(skill, skillNode);
            LoadAfterImages(skill, skillNode);

            var summonNode = ResolveSummonSourceProperty(skill, skillNode);
            if (summonNode != null)
            {
                LoadSummonAnimations(skill, summonNode);
            }

            var tileNode = skillNode["tile"];
            if (tileNode != null
                || !string.IsNullOrWhiteSpace(skill.ClientTileUolPath)
                || HasClientSkillAssetUolVariantPaths(skill.ClientCharacterLevelTileUolPaths)
                || HasClientSkillAssetUolVariantPaths(skill.ClientLevelTileUolPaths)
                || HasSkillZoneTileVariantNodes(skillNode))
            {
                skill.ZoneEffect = LoadZoneEffect(
                    tileNode,
                    skillNode,
                    skill.ClientTileUolPath,
                    skill.ClientCharacterLevelTileUolPaths,
                    skill.ClientLevelTileUolPaths);
                skill.ZoneAnimation = skill.ZoneEffect?.Animation;
                PopulateClientTileUolFallbackFromZoneEffect(skill);
            }

            // Load projectile/ball
            var ballNode = skillNode["ball"];
            if (ballNode != null
                || !string.IsNullOrWhiteSpace(skill.ClientBallUolPath)
                || !string.IsNullOrWhiteSpace(skill.ClientFlipBallUolPath))
            {
                skill.Projectile = LoadProjectile(
                    skill.SkillId,
                    ballNode,
                    skillNode,
                    skill.ClientBallUolPath,
                    skill.ClientFlipBallUolPath,
                    skill.ClientCharacterLevelBallUolPaths,
                    skill.ClientCharacterLevelFlipBallUolPaths,
                    skill.ClientLevelBallUolPaths,
                    skill.ClientLevelFlipBallUolPaths);
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

            foreach (string actionName in EnumerateDistinctMeleeAfterImageActionNames(skill?.SkillId ?? 0, actionNames))
            {
                if (chargeElement > 0)
                {
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

        private static IEnumerable<string> EnumerateDistinctMeleeAfterImageActionNames(
            int skillId,
            IEnumerable<string> actionNames)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string actionName in actionNames ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    continue;
                }

                string trimmedActionName = actionName.Trim();
                if (yielded.Add(trimmedActionName))
                {
                    yield return trimmedActionName;
                }

                if (!CharacterPart.TryGetClientRawActionCode(trimmedActionName, out int rawActionCode))
                {
                    continue;
                }

                foreach (string rangeLookupActionName in ClientMeleeAfterimageRangeResolver.EnumerateRangeLookupActionNames(
                             skillId,
                             rawActionCode))
                {
                    if (string.IsNullOrWhiteSpace(rangeLookupActionName))
                    {
                        continue;
                    }

                    string trimmedRangeLookupActionName = rangeLookupActionName.Trim();
                    if (yielded.Add(trimmedRangeLookupActionName))
                    {
                        yield return trimmedRangeLookupActionName;
                    }
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

                foreach (string requestedActionName in EnumerateDistinctMeleeAfterImageActionNames(skill?.SkillId ?? 0, actionNames ?? Array.Empty<string>()))
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
            string[] distinctActionNames = EnumerateDistinctMeleeAfterImageActionNames(skill?.SkillId ?? 0, actionNames ?? Array.Empty<string>()).ToArray();

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
                    MeleeAfterImageCatalog skillCatalog = skill?.GetAfterImageCatalogForCharacterLevel(weaponTypeKey, characterLevel);
                    if (skillCatalog?.Actions?.Count > 0)
                    {
                        yield return skillCatalog;
                    }

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

            AssignPersistentAvatarEffectPlane(
                skill,
                LoadAvatarEffectAnimation(skillNode, "special"),
                defaultUnderFace: false);
            AssignPersistentAvatarEffectPlane(
                skill,
                LoadAvatarEffectAnimation(skillNode, "special0"),
                defaultUnderFace: true);
            AssignSuddenDeathRepeatAvatarEffectPlane(skill, LoadSuddenDeathRepeatAnimation(skill, skillNode));
            skill.AvatarLadderEffect = LoadAvatarEffectAnimation(skillNode, "back");
            AssignPersistentAvatarFinishEffectPlane(
                skill,
                LoadAvatarEffectAnimation(skillNode, "finish"),
                defaultUnderFace: false);
            AssignPersistentAvatarFinishEffectPlane(
                skill,
                LoadAvatarEffectAnimation(skillNode, "finish0"),
                defaultUnderFace: true);
            skill.AvatarLadderFinishEffect = LoadAvatarEffectAnimation(skillNode, "back_finish");
        }

        internal static void AssignPersistentAvatarEffectPlaneForTesting(
            SkillData skill,
            SkillAnimation animation,
            bool defaultUnderFace)
        {
            AssignPersistentAvatarEffectPlane(skill, animation, defaultUnderFace);
        }

        private static void AssignPersistentAvatarEffectPlane(
            SkillData skill,
            SkillAnimation animation,
            bool defaultUnderFace)
        {
            if (skill == null || animation == null)
            {
                return;
            }

            if (defaultUnderFace || ClientOwnedAvatarEffectParity.PrefersUnderFaceAvatarEffectPlane(animation))
            {
                skill.AvatarUnderFaceEffect ??= animation;
                skill.AvatarUnderFaceSecondaryEffect ??= animation == skill.AvatarUnderFaceEffect ? null : animation;
                return;
            }

            skill.AvatarOverlayEffect ??= animation;
            skill.AvatarOverlaySecondaryEffect ??= animation == skill.AvatarOverlayEffect ? null : animation;
        }

        internal static void AssignPersistentAvatarFinishEffectPlaneForTesting(
            SkillData skill,
            SkillAnimation animation,
            bool defaultUnderFace)
        {
            AssignPersistentAvatarFinishEffectPlane(skill, animation, defaultUnderFace);
        }

        private static void AssignPersistentAvatarFinishEffectPlane(
            SkillData skill,
            SkillAnimation animation,
            bool defaultUnderFace)
        {
            if (skill == null || animation == null)
            {
                return;
            }

            if (defaultUnderFace || ClientOwnedAvatarEffectParity.PrefersUnderFaceAvatarEffectPlane(animation))
            {
                skill.AvatarUnderFaceFinishEffect ??= animation;
                return;
            }

            skill.AvatarOverlayFinishEffect ??= animation;
        }

        internal static void AssignSuddenDeathRepeatAvatarEffectPlaneForTesting(
            SkillData skill,
            SkillAnimation repeatAnimation)
        {
            AssignSuddenDeathRepeatAvatarEffectPlane(skill, repeatAnimation);
        }

        private static void AssignSuddenDeathRepeatAvatarEffectPlane(
            SkillData skill,
            SkillAnimation repeatAnimation)
        {
            if (skill == null || repeatAnimation == null)
            {
                return;
            }

            if (ClientOwnedAvatarEffectParity.PrefersUnderFaceAvatarEffectPlane(repeatAnimation))
            {
                skill.AvatarUnderFaceEffect ??= repeatAnimation;
            }
            else
            {
                skill.AvatarOverlayEffect ??= repeatAnimation;
            }
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
                SkillFrame directFrame = LoadAfterImageFrame(frameSetNode);
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

                SkillFrame frame = LoadAfterImageFrame(child);
                if (frame != null)
                {
                    frameSet.Frames.Add(frame);
                }
            }

            return frameSet;
        }

        private SkillFrame LoadAfterImageFrame(WzImageProperty frameNode)
        {
            return LoadSkillFrame(frameNode, ClientAfterimageCanvasDelayFallbackMs, false);
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
                39 => "barehands",
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

        private List<SkillAnimation> LoadIndexedSkillAnimations(WzImageProperty rootNode, string baseName)
        {
            var animations = new List<SkillAnimation>();
            if (rootNode == null)
            {
                return animations;
            }

            foreach (WzImageProperty child in rootNode.WzProperties)
            {
                if (child == null || !int.TryParse(child.Name, out int index) || index < 0)
                {
                    continue;
                }

                SkillAnimation animation = LoadSkillAnimation(child, $"{baseName}/{child.Name}");
                if (animation.Frames.Count <= 0)
                {
                    continue;
                }

                while (animations.Count <= index)
                {
                    animations.Add(null);
                }

                animations[index] = animation;
            }

            return animations;
        }

        private List<SkillAnimation> LoadPrefixedIndexedSkillAnimations(WzImageProperty rootNode, string prefix)
        {
            var animations = new List<SkillAnimation>();
            if (rootNode == null || string.IsNullOrWhiteSpace(prefix))
            {
                return animations;
            }

            foreach (WzImageProperty child in rootNode.WzProperties)
            {
                if (child == null
                    || string.IsNullOrWhiteSpace(child.Name)
                    || !child.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    || !int.TryParse(child.Name[prefix.Length..], out int index)
                    || index < 0)
                {
                    continue;
                }

                SkillAnimation animation = LoadSkillAnimation(child, child.Name);
                if (animation.Frames.Count <= 0)
                {
                    continue;
                }

                while (animations.Count <= index)
                {
                    animations.Add(null);
                }

                animations[index] = animation;
            }

            return animations;
        }

        private void PopulateTargetHitCharacterLevelVariants(SkillData skill, WzImageProperty charLevelNode)
        {
            if (skill == null || charLevelNode == null)
            {
                return;
            }

            foreach (WzImageProperty child in charLevelNode.WzProperties)
            {
                if (child == null || !int.TryParse(child.Name, out int requiredLevel))
                {
                    continue;
                }

                List<SkillAnimation> variants = LoadTargetHitVariantAnimations(
                    child["hit"],
                    $"hit/CharLevel/{requiredLevel}");
                if (variants.Count > 0)
                {
                    skill.CharacterLevelTargetHitEffects[requiredLevel] = variants;
                }
            }
        }

        private void PopulateTargetHitLevelVariants(SkillData skill, WzImageProperty levelNode)
        {
            if (skill == null || levelNode == null)
            {
                return;
            }

            foreach (WzImageProperty child in levelNode.WzProperties)
            {
                if (child == null || !int.TryParse(child.Name, out int skillLevel))
                {
                    continue;
                }

                List<SkillAnimation> variants = LoadTargetHitVariantAnimations(
                    child["hit"],
                    $"hit/level/{skillLevel}");
                if (variants.Count > 0)
                {
                    skill.LevelTargetHitEffects[skillLevel] = variants;
                }
            }
        }

        private List<SkillAnimation> LoadTargetHitVariantAnimations(WzImageProperty hitNode, string baseName)
        {
            if (hitNode == null)
            {
                return new List<SkillAnimation>();
            }

            List<SkillAnimation> indexedAnimations = LoadIndexedSkillAnimations(hitNode, baseName);
            if (indexedAnimations.Count > 0)
            {
                return indexedAnimations;
            }

            SkillAnimation animation = LoadSkillAnimation(hitNode, baseName);
            if (animation.Frames.Count <= 0)
            {
                return new List<SkillAnimation>();
            }

            return new List<SkillAnimation> { animation };
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

            PopulateShadowPartnerSupportedRawActionNames(skill, skillNode);
            SynthesizeCharacterOwnedShadowPartnerActionAnimations(
                skill.ShadowPartnerActionAnimations,
                skill.ShadowPartnerSupportedRawActionNames);
            SynthesizeClientOwnedShadowPartnerActionAnimations(
                skill.ShadowPartnerActionAnimations,
                skill.ShadowPartnerSupportedRawActionNames);
            SynthesizeMountedCharacterShadowPartnerActionAnimations(
                skill.ShadowPartnerActionAnimations,
                skill.ShadowPartnerSupportedRawActionNames);
            ApplyClientReplayTailsToShadowPartnerActionAnimations(skill.ShadowPartnerActionAnimations);
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

            List<WzImageProperty> orderedFrames = actionNode.WzProperties
                .Where(static child =>
                    child != null
                    && (child is WzCanvasProperty
                        || child.GetLinkedWzImageProperty() is WzCanvasProperty
                        || ContainsShadowPartnerInlineCanvasFrame(child)))
                .ToList();

            if (orderedFrames.Count == 0)
            {
                return Array.Empty<WzImageProperty>();
            }

            return orderedFrames;
        }

        private static bool ContainsShadowPartnerInlineCanvasFrame(WzImageProperty frameNode)
        {
            return frameNode?.WzProperties != null
                   && frameNode.WzProperties.Any(static child => child is WzCanvasProperty);
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

        private void PopulateShadowPartnerSupportedRawActionNames(SkillData skill, WzImageProperty skillNode)
        {
            if (skill == null)
            {
                return;
            }

            skill.ShadowPartnerSupportedRawActionNames.Clear();
            foreach (string rawActionName in EnumerateShadowPartnerRawActionNamesFromSourceSkillNodes(
                         EnumerateShadowPartnerRawActionSourceSkillNodes(skill, skillNode)))
            {
                skill.ShadowPartnerSupportedRawActionNames.Add(rawActionName);
            }
        }

        internal static IEnumerable<string> EnumerateShadowPartnerRawActionNamesFromSourceSkillNodes(
            IEnumerable<WzImageProperty> sourceSkillNodes)
        {
            if (sourceSkillNodes == null)
            {
                yield break;
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WzImageProperty sourceSkillNode in sourceSkillNodes)
            {
                foreach (WzImageProperty actionNode in EnumerateShadowPartnerSourceActionNodes(sourceSkillNode))
                {
                    string rawActionName = actionNode?.GetString();
                    if (!string.IsNullOrWhiteSpace(rawActionName) && yielded.Add(rawActionName))
                    {
                        yield return rawActionName;
                    }

                    if (actionNode?.WzProperties == null || actionNode.WzProperties.Count == 0)
                    {
                        continue;
                    }

                    foreach (WzImageProperty actionChild in EnumerateShadowPartnerClientActionPieceNodesInClientOrder(actionNode))
                    {
                        rawActionName = actionChild?.GetString();
                        if (!string.IsNullOrWhiteSpace(rawActionName) && yielded.Add(rawActionName))
                        {
                            yield return rawActionName;
                        }
                    }
                }
            }
        }

        private static IEnumerable<WzImageProperty> EnumerateShadowPartnerSourceActionNodes(WzImageProperty sourceSkillNode)
        {
            if (sourceSkillNode == null)
            {
                yield break;
            }

            WzImageProperty actionNode = sourceSkillNode["action"];
            if (actionNode != null)
            {
                yield return actionNode;
            }

            foreach (string nestedActionOwnerName in new[] { "prepare", "effect" })
            {
                WzImageProperty nestedActionNode = sourceSkillNode[nestedActionOwnerName]?["action"];
                if (nestedActionNode != null)
                {
                    yield return nestedActionNode;
                }
            }
        }

        private IEnumerable<WzImageProperty> EnumerateShadowPartnerRawActionSourceSkillNodes(
            SkillData skill,
            WzImageProperty skillNode)
        {
            var yieldedSkillIds = new HashSet<int>();
            if (skillNode != null && skill?.SkillId > 0 && yieldedSkillIds.Add(skill.SkillId))
            {
                yield return skillNode;
            }

            if (skill == null
                || !ShadowPartnerRawActionSourceSkillIdsBySkillId.TryGetValue(skill.SkillId, out int[] sourceSkillIds))
            {
                yield break;
            }

            foreach (int sourceSkillId in sourceSkillIds)
            {
                if (sourceSkillId <= 0
                    || !yieldedSkillIds.Add(sourceSkillId)
                    || !TryGetSkillNode(sourceSkillId, out WzImageProperty sourceSkillNode))
                {
                    continue;
                }

                yield return sourceSkillNode;
            }
        }

        internal static IReadOnlyList<int> GetShadowPartnerRawActionSourceSkillIdsForClientParity(int skillId)
        {
            return ShadowPartnerRawActionSourceSkillIdsBySkillId.TryGetValue(skillId, out int[] sourceSkillIds)
                ? Array.AsReadOnly(sourceSkillIds)
                : Array.Empty<int>();
        }

        internal static void SynthesizeClientOwnedShadowPartnerActionAnimations(
            IDictionary<string, SkillAnimation> actionAnimations,
            IReadOnlySet<string> supportedRawActionNames = null)
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
                    actionName,
                    supportedRawActionNames);
                if (piecedAnimation?.Frames.Count > 0)
                {
                    actionAnimations[actionName] = piecedAnimation;
                }
            }

            readOnlyActionAnimations =
                actionAnimations as IReadOnlyDictionary<string, SkillAnimation>
                ?? new Dictionary<string, SkillAnimation>(actionAnimations, StringComparer.OrdinalIgnoreCase);

            foreach (string actionName in ShadowPartnerClientActionResolver.EnumerateRemappedShadowPartnerActionNames())
            {
                if (string.IsNullOrWhiteSpace(actionName) || actionAnimations.ContainsKey(actionName))
                {
                    continue;
                }

                SkillAnimation remappedAnimation = ShadowPartnerClientActionResolver.TryBuildRemappedShadowPartnerActionAnimation(
                    readOnlyActionAnimations,
                    actionName,
                    supportedRawActionNames);
                if (remappedAnimation?.Frames.Count > 0)
                {
                    actionAnimations[actionName] = remappedAnimation;
                }
            }
        }

        private void SynthesizeCharacterOwnedShadowPartnerActionAnimations(
            IDictionary<string, SkillAnimation> actionAnimations,
            IReadOnlySet<string> supportedRawActionNames)
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
                if (string.IsNullOrWhiteSpace(actionName)
                    || actionAnimations.ContainsKey(actionName)
                    || !TryGetShadowPartnerClientActionPieces(actionName, out IReadOnlyList<ShadowPartnerClientActionResolver.ShadowPartnerActionPiece> piecePlan))
                {
                    continue;
                }

                SkillAnimation piecedAnimation = ShadowPartnerClientActionResolver.TryBuildPiecedShadowPartnerActionAnimation(
                    readOnlyActionAnimations,
                    actionName,
                    supportedRawActionNames,
                    piecePlanOverride: piecePlan,
                    requireSupportedRawActionName: true);
                if (piecedAnimation?.Frames.Count > 0)
                {
                    actionAnimations[actionName] = piecedAnimation;
                }
            }

            readOnlyActionAnimations =
                actionAnimations as IReadOnlyDictionary<string, SkillAnimation>
                ?? new Dictionary<string, SkillAnimation>(actionAnimations, StringComparer.OrdinalIgnoreCase);

            foreach (string actionName in ShadowPartnerClientActionResolver.EnumerateRemappedShadowPartnerActionNames())
            {
                if (string.IsNullOrWhiteSpace(actionName)
                    || actionAnimations.ContainsKey(actionName)
                    || !TryGetShadowPartnerClientActionPieces(actionName, out IReadOnlyList<ShadowPartnerClientActionResolver.ShadowPartnerActionPiece> piecePlan))
                {
                    continue;
                }

                SkillAnimation piecedAnimation = ShadowPartnerClientActionResolver.TryBuildPiecedShadowPartnerActionAnimation(
                    readOnlyActionAnimations,
                    actionName,
                    supportedRawActionNames,
                    piecePlanOverride: piecePlan,
                    requireSupportedRawActionName: false);
                if (piecedAnimation?.Frames.Count > 0)
                {
                    actionAnimations[actionName] = piecedAnimation;
                }
            }

            readOnlyActionAnimations =
                actionAnimations as IReadOnlyDictionary<string, SkillAnimation>
                ?? new Dictionary<string, SkillAnimation>(actionAnimations, StringComparer.OrdinalIgnoreCase);

            foreach (string actionName in ShadowPartnerClientActionResolver.EnumerateCharacterOwnedMountedActionCandidateNames(supportedRawActionNames))
            {
                if (string.IsNullOrWhiteSpace(actionName)
                    || actionAnimations.ContainsKey(actionName)
                    || !TryGetShadowPartnerClientActionPieces(actionName, out IReadOnlyList<ShadowPartnerClientActionResolver.ShadowPartnerActionPiece> piecePlan))
                {
                    continue;
                }

                SkillAnimation piecedAnimation = ShadowPartnerClientActionResolver.TryBuildPiecedShadowPartnerActionAnimation(
                    readOnlyActionAnimations,
                    actionName,
                    supportedRawActionNames,
                    piecePlanOverride: piecePlan,
                    requireSupportedRawActionName: false);
                if (piecedAnimation?.Frames.Count > 0)
                {
                    actionAnimations[actionName] = piecedAnimation;
                }
            }

            readOnlyActionAnimations =
                actionAnimations as IReadOnlyDictionary<string, SkillAnimation>
                ?? new Dictionary<string, SkillAnimation>(actionAnimations, StringComparer.OrdinalIgnoreCase);

            foreach (string actionName in ShadowPartnerClientActionResolver.EnumerateClientInitializedFallbackActionNames())
            {
                if (string.IsNullOrWhiteSpace(actionName)
                    || actionAnimations.ContainsKey(actionName)
                    || !TryGetShadowPartnerClientActionPieces(actionName, out IReadOnlyList<ShadowPartnerClientActionResolver.ShadowPartnerActionPiece> piecePlan))
                {
                    continue;
                }

                SkillAnimation piecedAnimation = ShadowPartnerClientActionResolver.TryBuildPiecedShadowPartnerActionAnimation(
                    readOnlyActionAnimations,
                    actionName,
                    supportedRawActionNames,
                    piecePlanOverride: piecePlan,
                    requireSupportedRawActionName: false);
                if (piecedAnimation?.Frames.Count > 0)
                {
                    actionAnimations[actionName] = piecedAnimation;
                }
            }
        }

        private void SynthesizeMountedCharacterShadowPartnerActionAnimations(
            IDictionary<string, SkillAnimation> actionAnimations,
            IReadOnlySet<string> supportedRawActionNames)
        {
            if (actionAnimations == null || actionAnimations.Count == 0)
            {
                return;
            }

            WzImage actionImage = global::HaCreator.Program.FindImage("Character", "0000/00002000");
            if (actionImage?.WzProperties == null || actionImage.WzProperties.Count == 0)
            {
                return;
            }

            IReadOnlyDictionary<string, SkillAnimation> readOnlyActionAnimations =
                actionAnimations as IReadOnlyDictionary<string, SkillAnimation>
                ?? new Dictionary<string, SkillAnimation>(actionAnimations, StringComparer.OrdinalIgnoreCase);

            foreach (string actionName in ShadowPartnerClientActionResolver.EnumerateMountedShadowPartnerActionCandidateNames(supportedRawActionNames))
            {
                WzImageProperty actionNode = ResolveLinkedProperty(actionImage[actionName]);
                if (string.IsNullOrWhiteSpace(actionName)
                    || actionNode?.WzProperties == null
                    || actionNode.WzProperties.Count == 0
                    || actionAnimations.ContainsKey(actionName)
                    || !TryGetShadowPartnerClientActionPieces(
                        actionName,
                        out IReadOnlyList<ShadowPartnerClientActionResolver.ShadowPartnerActionPiece> piecePlan))
                {
                    continue;
                }

                SkillAnimation piecedAnimation = ShadowPartnerClientActionResolver.TryBuildPiecedShadowPartnerActionAnimation(
                    readOnlyActionAnimations,
                    actionName,
                    supportedRawActionNames,
                    piecePlanOverride: piecePlan,
                    requireSupportedRawActionName: false);
                if (piecedAnimation?.Frames.Count > 0)
                {
                    actionAnimations[actionName] = piecedAnimation;
                    readOnlyActionAnimations =
                        actionAnimations as IReadOnlyDictionary<string, SkillAnimation>
                        ?? new Dictionary<string, SkillAnimation>(actionAnimations, StringComparer.OrdinalIgnoreCase);
                }
            }

            // CActionMan::Init still seeds helper rows from Character/00002000.img before
            // LoadShadowPartnerAction resolves plain raw-action fallback. Keep the recovered
            // explicit candidate order above, then admit any additional mounted non-ghost
            // helper rows that are structurally parsable from the same action surface.
            foreach (WzImageProperty actionChild in actionImage.WzProperties)
            {
                string actionName = actionChild?.Name;
                if (string.IsNullOrWhiteSpace(actionName)
                    || actionAnimations.ContainsKey(actionName)
                    || actionName.StartsWith("ghost", StringComparison.OrdinalIgnoreCase)
                    || ShadowPartnerClientActionResolver.IsFamilyGatedMountedAliasActionName(actionName))
                {
                    continue;
                }

                if (TryGetShadowPartnerClientActionPieces(
                        actionName,
                        out IReadOnlyList<ShadowPartnerClientActionResolver.ShadowPartnerActionPiece> piecePlan))
                {
                    SkillAnimation piecedAnimation = ShadowPartnerClientActionResolver.TryBuildPiecedShadowPartnerActionAnimation(
                        readOnlyActionAnimations,
                        actionName,
                        supportedRawActionNames,
                        piecePlanOverride: piecePlan,
                        requireSupportedRawActionName: false);
                    if (piecedAnimation?.Frames.Count > 0)
                    {
                        actionAnimations[actionName] = piecedAnimation;
                        readOnlyActionAnimations =
                            actionAnimations as IReadOnlyDictionary<string, SkillAnimation>
                            ?? new Dictionary<string, SkillAnimation>(actionAnimations, StringComparer.OrdinalIgnoreCase);
                    }

                    continue;
                }

                if (ShadowPartnerClientActionResolver.IsMountedCreateActionFrameName(actionName))
                {
                    WzImageProperty actionNode = ResolveLinkedProperty(actionChild);
                    SkillAnimation mountedFrameAnimation = LoadShadowPartnerActionAnimation(actionNode, actionName);
                    if (mountedFrameAnimation?.Frames.Count > 0)
                    {
                        actionAnimations[actionName] = mountedFrameAnimation;
                        readOnlyActionAnimations =
                            actionAnimations as IReadOnlyDictionary<string, SkillAnimation>
                            ?? new Dictionary<string, SkillAnimation>(actionAnimations, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }

            SynthesizeFallbackRemappedMountedShadowPartnerActions(actionAnimations, supportedRawActionNames);
        }

        internal static void SynthesizeFallbackRemappedMountedShadowPartnerActions(
            IDictionary<string, SkillAnimation> actionAnimations,
            IReadOnlySet<string> supportedRawActionNames = null)
        {
            if (actionAnimations == null || actionAnimations.Count == 0)
            {
                return;
            }

            IReadOnlyDictionary<string, SkillAnimation> readOnlyActionAnimations =
                actionAnimations as IReadOnlyDictionary<string, SkillAnimation>
                ?? new Dictionary<string, SkillAnimation>(actionAnimations, StringComparer.OrdinalIgnoreCase);

            // `LoadShadowPartnerAction` still falls back to plain action-name lookup when an
            // action-specific helper row is missing from Character/00002000.img. Keep mounted
            // piece rows as first priority, then synthesize built-in piece plans or remapped
            // aliases for any remaining client-initialized rows.
            foreach (string actionName in ShadowPartnerClientActionResolver.EnumerateClientInitializedFallbackActionNames())
            {
                if (string.IsNullOrWhiteSpace(actionName)
                    || actionAnimations.ContainsKey(actionName)
                    || ShadowPartnerClientActionResolver.IsFamilyGatedMountedAliasActionName(actionName)
                    || !ShadowPartnerClientActionResolver.ShouldSynthesizeClientInitializedFallbackAction(actionName))
                {
                    continue;
                }

                SkillAnimation piecedAnimation = ShadowPartnerClientActionResolver.TryBuildPiecedShadowPartnerActionAnimation(
                    readOnlyActionAnimations,
                    actionName,
                    supportedRawActionNames,
                    piecePlanOverride: null,
                    requireSupportedRawActionName: false);
                if (piecedAnimation?.Frames.Count > 0)
                {
                    actionAnimations[actionName] = piecedAnimation;
                    readOnlyActionAnimations =
                        actionAnimations as IReadOnlyDictionary<string, SkillAnimation>
                        ?? new Dictionary<string, SkillAnimation>(actionAnimations, StringComparer.OrdinalIgnoreCase);
                    continue;
                }

                SkillAnimation remappedAnimation = ShadowPartnerClientActionResolver.TryBuildRemappedShadowPartnerActionAnimation(
                    readOnlyActionAnimations,
                    actionName,
                    supportedRawActionNames);
                if (remappedAnimation?.Frames.Count > 0)
                {
                    actionAnimations[actionName] = remappedAnimation;
                    readOnlyActionAnimations =
                        actionAnimations as IReadOnlyDictionary<string, SkillAnimation>
                        ?? new Dictionary<string, SkillAnimation>(actionAnimations, StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        private bool TryGetShadowPartnerClientActionPieces(
            string actionName,
            out IReadOnlyList<ShadowPartnerClientActionResolver.ShadowPartnerActionPiece> piecePlan)
        {
            piecePlan = null;
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (_shadowPartnerClientActionPieceCache.TryGetValue(actionName, out IReadOnlyList<ShadowPartnerClientActionResolver.ShadowPartnerActionPiece> cachedPlan))
            {
                piecePlan = cachedPlan;
                return true;
            }

            if (_missingShadowPartnerClientActionPieceKeys.Contains(actionName))
            {
                return false;
            }

            WzImage actionImage = global::HaCreator.Program.FindImage("Character", "0000/00002000");
            WzImageProperty actionNode = ResolveLinkedProperty(actionImage?[actionName]);
            if (actionNode?.WzProperties == null || actionNode.WzProperties.Count == 0)
            {
                _missingShadowPartnerClientActionPieceKeys.Add(actionName);
                return false;
            }

            if (!TryParseShadowPartnerClientActionPieces(actionName, actionNode, out IReadOnlyList<ShadowPartnerClientActionResolver.ShadowPartnerActionPiece> parsedPieces))
            {
                _missingShadowPartnerClientActionPieceKeys.Add(actionName);
                return false;
            }

            piecePlan = parsedPieces;
            _shadowPartnerClientActionPieceCache[actionName] = piecePlan;
            return true;
        }

        internal static bool TryParseShadowPartnerClientActionPieces(
            WzImageProperty actionNode,
            out IReadOnlyList<ShadowPartnerClientActionResolver.ShadowPartnerActionPiece> piecePlan)
        {
            return TryParseShadowPartnerClientActionPieces(null, actionNode, out piecePlan);
        }

        internal static bool TryParseShadowPartnerClientActionPieces(
            string actionName,
            WzImageProperty actionNode,
            out IReadOnlyList<ShadowPartnerClientActionResolver.ShadowPartnerActionPiece> piecePlan)
        {
            piecePlan = null;
            WzImageProperty pieceOwnerNode = ShadowPartnerClientActionResolver.ResolveClientActionManInitPieceOwnerNode(
                actionName,
                actionNode);
            if (pieceOwnerNode?.WzProperties == null || pieceOwnerNode.WzProperties.Count == 0)
            {
                return false;
            }

            var pieces = new List<ShadowPartnerClientActionResolver.ShadowPartnerActionPiece>(pieceOwnerNode.WzProperties.Count);
            int fallbackSlotIndex = 0;
            foreach (WzImageProperty pieceNode in EnumerateShadowPartnerClientActionPieceNodesInClientOrder(pieceOwnerNode))
            {
                if (pieceNode == null)
                {
                    continue;
                }

                string pieceActionName = pieceNode["action"]?.GetString();
                if (string.IsNullOrWhiteSpace(pieceActionName))
                {
                    continue;
                }

                pieces.Add(new ShadowPartnerClientActionResolver.ShadowPartnerActionPiece(
                    ResolveShadowPartnerClientActionPieceSlotIndex(pieceNode.Name, fallbackSlotIndex++),
                    pieceActionName,
                    GetInt(pieceNode, "frame"),
                    pieceNode["delay"] != null ? GetInt(pieceNode, "delay") : ShadowPartnerClientActionResolver.ClientActionManInitDefaultPieceDelayMs,
                    ResolveShadowPartnerClientActionPieceFlip(pieceNode),
                    GetVector(pieceNode, "move"),
                    GetInt(pieceNode, "rotate"),
                    IsSyntheticMirroredTailPiece: false,
                    IsClientActionManInitPiece: true,
                    EventDelayOverrideMs: ResolveShadowPartnerClientActionPieceEventDelayMs(pieceNode),
                    InlineCanvasChildNames: EnumerateShadowPartnerClientActionPieceInlineCanvasChildNames(pieceNode)));
            }

            if (pieces.Count == 0 && ShadowPartnerClientActionResolver.IsClientActionPieceNode(pieceOwnerNode))
            {
                pieces.Add(new ShadowPartnerClientActionResolver.ShadowPartnerActionPiece(
                    0,
                    pieceOwnerNode["action"]?.GetString(),
                    GetInt(pieceOwnerNode, "frame"),
                    pieceOwnerNode["delay"] != null ? GetInt(pieceOwnerNode, "delay") : ShadowPartnerClientActionResolver.ClientActionManInitDefaultPieceDelayMs,
                    ResolveShadowPartnerClientActionPieceFlip(pieceOwnerNode),
                    GetVector(pieceOwnerNode, "move"),
                    GetInt(pieceOwnerNode, "rotate"),
                    IsSyntheticMirroredTailPiece: false,
                    IsClientActionManInitPiece: true,
                    EventDelayOverrideMs: ResolveShadowPartnerClientActionPieceEventDelayMs(pieceOwnerNode),
                    InlineCanvasChildNames: EnumerateShadowPartnerClientActionPieceInlineCanvasChildNames(pieceOwnerNode)));
            }

            if (pieces.Count == 0)
            {
                return false;
            }

            if (GetInt(pieceOwnerNode, "zigzag") != 0)
            {
                AppendClientZigZagShadowPartnerActionPieces(pieces);
            }

            piecePlan = pieces.AsReadOnly();
            return true;
        }

        private static void AppendClientZigZagShadowPartnerActionPieces(
            List<ShadowPartnerClientActionResolver.ShadowPartnerActionPiece> pieces)
        {
            if (pieces == null || pieces.Count < 3)
            {
                return;
            }

            int originalCount = pieces.Count;
            int nextSlotIndex = pieces.Max(static piece => piece.SlotIndex) + 1;
            for (int sourceIndex = originalCount - 2; sourceIndex >= 1; sourceIndex--)
            {
                ShadowPartnerClientActionResolver.ShadowPartnerActionPiece sourcePiece = pieces[sourceIndex];
                pieces.Add(new ShadowPartnerClientActionResolver.ShadowPartnerActionPiece(
                    nextSlotIndex++,
                    sourcePiece.PieceActionName,
                    // CActionMan::Init zeroes mirrored zigzag tail frame indices after copying.
                    0,
                    sourcePiece.DelayOverrideMs,
                    sourcePiece.Flip,
                    sourcePiece.Move,
                    sourcePiece.RotationDegrees,
                    IsSyntheticMirroredTailPiece: true,
                    IsClientActionManInitPiece: sourcePiece.IsClientActionManInitPiece,
                    EventDelayOverrideMs: sourcePiece.EventDelayOverrideMs,
                    InlineCanvasChildNames: sourcePiece.InlineCanvasChildNames));
            }
        }

        private static IReadOnlyList<string> EnumerateShadowPartnerClientActionPieceInlineCanvasChildNames(WzImageProperty pieceNode)
        {
            if (pieceNode?.WzProperties == null || pieceNode.WzProperties.Count == 0)
            {
                return Array.Empty<string>();
            }

            var canvasChildNames = new List<string>();
            foreach (WzImageProperty child in pieceNode.WzProperties)
            {
                if (child is WzCanvasProperty && !string.IsNullOrWhiteSpace(child.Name))
                {
                    canvasChildNames.Add(child.Name);
                }
            }

            return canvasChildNames.Count > 0 ? canvasChildNames.AsReadOnly() : Array.Empty<string>();
        }

        private static int? ResolveShadowPartnerClientActionPieceEventDelayMs(WzImageProperty pieceNode)
        {
            if (pieceNode == null)
            {
                return null;
            }

            if (pieceNode["event-delay"] != null)
            {
                return GetInt(pieceNode, "event-delay");
            }

            if (pieceNode["eventDelay"] != null)
            {
                return GetInt(pieceNode, "eventDelay");
            }

            return null;
        }

        private static bool ResolveShadowPartnerClientActionPieceFlip(WzImageProperty pieceNode)
        {
            if (pieceNode == null)
            {
                return false;
            }

            if (pieceNode["flip"] != null)
            {
                return GetInt(pieceNode, "flip") != 0;
            }

            // Character/00002000/fatalBlow/5 publishes the client-init flip flag as
            // `filp`; keep it in the loader parser instead of normalizing the WZ row.
            return pieceNode["filp"] != null && GetInt(pieceNode, "filp") != 0;
        }

        private static int ResolveShadowPartnerClientActionPieceSlotIndex(string pieceName, int fallbackSlotIndex)
        {
            return !string.IsNullOrWhiteSpace(pieceName) && int.TryParse(pieceName, out int slotIndex)
                ? slotIndex
                : fallbackSlotIndex;
        }

        private static IEnumerable<WzImageProperty> EnumerateShadowPartnerClientActionPieceNodesInClientOrder(WzImageProperty actionNode)
        {
            if (actionNode?.WzProperties == null || actionNode.WzProperties.Count == 0)
            {
                return Array.Empty<WzImageProperty>();
            }

            var numericChildren = new List<(int Index, WzImageProperty Node)>();
            var nonNumericChildren = new List<WzImageProperty>();
            foreach (WzImageProperty child in actionNode.WzProperties)
            {
                if (child == null)
                {
                    continue;
                }

                if (int.TryParse(child.Name, out int index))
                {
                    numericChildren.Add((index, child));
                }
                else
                {
                    nonNumericChildren.Add(child);
                }
            }

            if (numericChildren.Count == 0)
            {
                return nonNumericChildren;
            }

            return numericChildren
                .OrderBy(static child => child.Index)
                .Select(static child => child.Node)
                .Concat(nonNumericChildren);
        }

        internal static void ApplyClientReplayTailsToShadowPartnerActionAnimations(
            IDictionary<string, SkillAnimation> actionAnimations)
        {
            if (actionAnimations == null || actionAnimations.Count == 0)
            {
                return;
            }

            foreach ((string actionName, SkillAnimation animation) in actionAnimations.ToArray())
            {
                if (ShouldAppendReversedShadowPartnerFrames(actionName))
                {
                    AppendReversedInteriorShadowPartnerFrames(animation);
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
            return ShadowPartnerClientActionResolver.ShouldLoopShadowPartnerAction(actionName);
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
            if (GetInt(node, "flip") != 0)
            {
                ApplyBranchFlipToAnimationFrames(animation);
            }

            return animation;
        }

        private static void ApplyBranchFlipToAnimationFrames(SkillAnimation animation)
        {
            if (animation?.Frames == null)
            {
                return;
            }

            foreach (SkillFrame frame in animation.Frames)
            {
                if (frame != null)
                {
                    frame.Flip = !frame.Flip;
                }
            }
        }

        internal static void ApplyBranchFlipToAnimationFramesForTesting(SkillAnimation animation)
        {
            ApplyBranchFlipToAnimationFrames(animation);
        }

        private ZoneEffectData LoadZoneEffect(
            WzImageProperty tileNode,
            WzImageProperty skillNode,
            string explicitTileUolPath = null,
            IReadOnlyDictionary<int, string> explicitCharacterLevelTileUolPaths = null,
            IReadOnlyDictionary<int, string> explicitLevelTileUolPaths = null)
        {
            bool hasExplicitTileVariantPaths = HasClientSkillAssetUolVariantPaths(explicitCharacterLevelTileUolPaths)
                || HasClientSkillAssetUolVariantPaths(explicitLevelTileUolPaths);
            bool hasVisibleTileVariantNodes = HasSkillZoneTileVariantNodes(skillNode);
            if (tileNode == null
                && string.IsNullOrWhiteSpace(explicitTileUolPath)
                && !hasExplicitTileVariantPaths
                && !hasVisibleTileVariantNodes)
            {
                return null;
            }

            WzImageProperty resolvedTileNode = tileNode;
            if (resolvedTileNode == null
                && !string.IsNullOrWhiteSpace(explicitTileUolPath)
                && TryResolveSkillPropertyPath(explicitTileUolPath, out WzImageProperty explicitTileNode))
            {
                resolvedTileNode = explicitTileNode;
            }

            var zoneEffect = new ZoneEffectData
            {
                Animation = LoadZoneAnimation(resolvedTileNode),
                AnimationPath = LoadZoneAnimationPath(resolvedTileNode),
                TileUolPath = !string.IsNullOrWhiteSpace(explicitTileUolPath)
                    ? explicitTileUolPath
                    : NormalizeSkillAssetPath(resolvedTileNode),
                VariantAnimations = LoadSummonIndexedAnimations(resolvedTileNode, "tile"),
                VariantAnimationPaths = LoadSummonIndexedAnimationPaths(resolvedTileNode),
                EffectDistance = GetInt(resolvedTileNode, "effectDistance")
            };

            if (zoneEffect.Animation?.Frames.Count <= 0)
            {
                zoneEffect.Animation = !string.IsNullOrWhiteSpace(explicitTileUolPath)
                    ? LoadSkillAnimationByNormalizedPath(explicitTileUolPath)
                    : null;
            }

            if (zoneEffect.Animation?.Frames.Count <= 0)
            {
                zoneEffect.Animation = zoneEffect.ResolveAnimationVariant(1, 1);
            }

            PopulateZoneCharacterLevelVariants(zoneEffect, skillNode?["CharLevel"]);
            PopulateZoneLevelVariants(zoneEffect, skillNode?["level"]);
            ApplyClientSkillAssetUolVariantPaths(
                zoneEffect.CharacterLevelTileUolPaths,
                explicitCharacterLevelTileUolPaths);
            ApplyClientSkillAssetUolVariantPaths(
                zoneEffect.LevelTileUolPaths,
                explicitLevelTileUolPaths);
            PopulateExplicitZoneTilePathVariants(
                zoneEffect,
                explicitCharacterLevelTileUolPaths,
                zoneEffect.CharacterLevelVariantAnimations,
                zoneEffect.CharacterLevelVariantAnimationPaths,
                zoneEffect.CharacterLevelEffectDistances);
            PopulateExplicitZoneTilePathVariants(
                zoneEffect,
                explicitLevelTileUolPaths,
                zoneEffect.LevelVariantAnimations,
                zoneEffect.LevelVariantAnimationPaths,
                zoneEffect.LevelEffectDistances);

            if (!string.IsNullOrWhiteSpace(explicitTileUolPath))
            {
                zoneEffect.AnimationPath ??= explicitTileUolPath;
            }

            bool hasRenderableAnimation = zoneEffect.Animation?.Frames.Count > 0
                || zoneEffect.VariantAnimations.Count > 0
                || zoneEffect.CharacterLevelVariantAnimations.Count > 0
                || zoneEffect.LevelVariantAnimations.Count > 0;
            bool hasClientPathFallback = !string.IsNullOrWhiteSpace(zoneEffect.TileUolPath)
                || !string.IsNullOrWhiteSpace(zoneEffect.AnimationPath);
            return hasRenderableAnimation || hasClientPathFallback ? zoneEffect : null;
        }

        private static bool HasClientSkillAssetUolVariantPaths(IReadOnlyDictionary<int, string> paths)
        {
            if (paths == null || paths.Count == 0)
            {
                return false;
            }

            foreach (KeyValuePair<int, string> entry in paths)
            {
                if (entry.Key > 0 && !string.IsNullOrWhiteSpace(entry.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasSkillZoneTileVariantNodes(WzImageProperty skillNode)
        {
            return HasZoneTileVariantBranchNodes(skillNode?["CharLevel"])
                   || HasZoneTileVariantBranchNodes(skillNode?["level"]);
        }

        private static bool HasZoneTileVariantBranchNodes(WzImageProperty variantRootNode)
        {
            if (variantRootNode?.WzProperties == null)
            {
                return false;
            }

            foreach (WzImageProperty child in variantRootNode.WzProperties)
            {
                if (child?["tile"] != null)
                {
                    return true;
                }
            }

            return false;
        }

        internal ZoneEffectData LoadZoneEffectForTest(
            WzImageProperty tileNode,
            WzImageProperty skillNode,
            string explicitTileUolPath = null,
            IReadOnlyDictionary<int, string> explicitCharacterLevelTileUolPaths = null,
            IReadOnlyDictionary<int, string> explicitLevelTileUolPaths = null)
        {
            return LoadZoneEffect(
                tileNode,
                skillNode,
                explicitTileUolPath,
                explicitCharacterLevelTileUolPaths,
                explicitLevelTileUolPaths);
        }

        private void PopulateExplicitZoneTilePathVariants(
            ZoneEffectData zoneEffect,
            IReadOnlyDictionary<int, string> explicitTileUolPaths,
            IDictionary<int, List<SkillAnimation>> targetAnimations,
            IDictionary<int, List<string>> targetAnimationPaths,
            IDictionary<int, int> targetEffectDistances)
        {
            if (zoneEffect == null
                || explicitTileUolPaths == null
                || explicitTileUolPaths.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<int, string> entry in explicitTileUolPaths)
            {
                if (entry.Key <= 0
                    || string.IsNullOrWhiteSpace(entry.Value)
                    || !TryResolveSkillPropertyPath(entry.Value, out WzImageProperty explicitTileNode))
                {
                    continue;
                }

                List<SkillAnimation> variants = LoadSummonIndexedAnimations(explicitTileNode, "tile");
                if (variants.Count == 0)
                {
                    SkillAnimation animation = LoadZoneAnimation(explicitTileNode);
                    if (animation?.Frames.Count > 0)
                    {
                        variants.Add(animation);
                    }
                }

                if (variants.Count > 0)
                {
                    targetAnimations[entry.Key] = variants;
                }

                List<string> variantPaths = LoadSummonIndexedAnimationPaths(explicitTileNode);
                if (variantPaths.Count == 0)
                {
                    variantPaths.Add(entry.Value);
                }

                targetAnimationPaths[entry.Key] = variantPaths;
                int effectDistance = GetInt(explicitTileNode, "effectDistance");
                if (effectDistance > 0)
                {
                    targetEffectDistances[entry.Key] = effectDistance;
                }
            }
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
            LoadSummonOwnedEffectAnimations(skill, summonNode);

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
            skill.SummonProjectileAnimationPaths = LoadSummonIndexedAnimationPaths(summonNode["ball"]);
            skill.SummonTargetHitPresentations = LoadSummonImpactPresentations(summonNode["mob"], "mob");

            if (attackBranch != null)
            {
                List<SkillAnimation> attackBranchProjectileAnimations = BuildSummonBranchProjectileAnimations(
                    skill.SummonProjectileAnimations,
                    LoadSummonIndexedAnimations(attackBranch["info"]?["ball"], $"{attackBranchName}/info/ball"));
                List<string> attackBranchProjectileAnimationPaths = BuildSummonBranchProjectileAnimationPaths(
                    skill.SummonProjectileAnimationPaths,
                    LoadSummonIndexedAnimationPaths(attackBranch["info"]?["ball"]));
                List<SummonImpactPresentation> attackBranchImpactPresentations = BuildSummonBranchImpactPresentations(
                    skill.SummonTargetHitPresentations,
                    LoadSummonImpactPresentations(attackBranch["info"]?["mob"], $"{attackBranchName}/info/mob"));
                skill.SummonProjectileAnimations = attackBranchProjectileAnimations;
                skill.SummonProjectileAnimationPaths = attackBranchProjectileAnimationPaths;
                skill.SummonTargetHitPresentations = attackBranchImpactPresentations;
                RegisterSummonBranchImpactMetadata(
                    skill,
                    attackBranchName,
                    attackBranchProjectileAnimations,
                    attackBranchProjectileAnimationPaths,
                    attackBranchImpactPresentations);
            }

            if (removalBranch != null)
            {
                AppendSummonIndexedAnimations(
                    skill.SummonProjectileAnimations,
                    LoadSummonIndexedAnimations(removalBranch["info"]?["ball"], $"{removalBranchName}/info/ball"));
                AppendSummonIndexedAnimationPaths(
                    skill.SummonProjectileAnimationPaths,
                    LoadSummonIndexedAnimationPaths(removalBranch["info"]?["ball"]));
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

        private void LoadSummonOwnedEffectAnimations(SkillData skill, WzImageProperty summonNode)
        {
            if (skill == null || summonNode == null)
            {
                return;
            }

            foreach (WzImageProperty branchNode in summonNode.WzProperties)
            {
                if (branchNode == null || !IsClientSummonOwnedEffectBranchName(branchNode.Name))
                {
                    continue;
                }

                SkillAnimation animation = LoadSkillAnimation(branchNode, branchNode.Name);
                if (animation?.Frames.Count <= 0)
                {
                    continue;
                }

                animation.Loop = IsClientSummonOwnedLoopingEffectBranchName(branchNode.Name) || animation.Loop;
                skill.SummonNamedAnimations[branchNode.Name] = animation;
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
                zoneEffect.CharacterLevelTileUolPaths[requiredLevel] = NormalizeSkillAssetPath(tileVariantNode);
                zoneEffect.CharacterLevelEffectDistances[requiredLevel] = GetInt(tileVariantNode, "effectDistance");
                if (variants.Count > 0)
                {
                    zoneEffect.CharacterLevelVariantAnimations[requiredLevel] = variants;
                    zoneEffect.CharacterLevelVariantAnimationPaths[requiredLevel] = LoadSummonIndexedAnimationPaths(tileVariantNode);
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
                zoneEffect.LevelTileUolPaths[skillLevel] = NormalizeSkillAssetPath(tileVariantNode);
                zoneEffect.LevelEffectDistances[skillLevel] = GetInt(tileVariantNode, "effectDistance");
                if (variants.Count > 0)
                {
                    zoneEffect.LevelVariantAnimations[skillLevel] = variants;
                    zoneEffect.LevelVariantAnimationPaths[skillLevel] = LoadSummonIndexedAnimationPaths(tileVariantNode);
                }
            }
        }

        internal bool TryResolveSummonSourceProperty(
            SkillData skill,
            WzImageProperty skillNode,
            out WzImageProperty summonNode)
        {
            summonNode = null;
            foreach (SummonSourceCandidate candidate in EnumerateSummonSourceCandidates(skill, skillNode))
            {
                foreach (string summonedUolPath in EnumerateClientSummonedUolSourcePaths(candidate.Skill))
                {
                    if (TryResolveClientSummonedUolProperty(summonedUolPath, out summonNode))
                    {
                        return true;
                    }
                }

                if (TryResolveSummonSourcePropertyFromSkillNode(candidate.SkillNode, out summonNode))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateClientSummonedUolSourcePaths(SkillData skill)
        {
            if (skill == null)
            {
                yield break;
            }

            var yieldedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (skill.ClientSummonedUolCandidatePaths != null)
            {
                foreach (string candidatePath in skill.ClientSummonedUolCandidatePaths)
                {
                    if (!string.IsNullOrWhiteSpace(candidatePath) && yieldedPaths.Add(candidatePath))
                    {
                        yield return candidatePath;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(skill.ClientSummonedUolPath)
                && yieldedPaths.Add(skill.ClientSummonedUolPath))
            {
                yield return skill.ClientSummonedUolPath;
            }
        }

        private IEnumerable<SummonSourceCandidate> EnumerateSummonSourceCandidates(SkillData skill, WzImageProperty skillNode)
        {
            var yieldedSkillIds = new HashSet<int>();
            if (skillNode != null)
            {
                int currentSkillId = skill?.SkillId ?? 0;
                yield return new SummonSourceCandidate(currentSkillId, skill, skillNode);
                if (currentSkillId > 0)
                {
                    yieldedSkillIds.Add(currentSkillId);
                }
            }

            foreach (int linkedSkillId in EnumerateSummonSourceCandidateSkillIds(
                         skill,
                         LoadSummonSourceCandidateSkillMetadata,
                         FindVisibleSummonSourceParentIds))
            {
                if (linkedSkillId <= 0 || !yieldedSkillIds.Add(linkedSkillId))
                {
                    continue;
                }

                if (TryGetSkillNode(linkedSkillId, out WzImageProperty linkedSkillNode))
                {
                    yield return new SummonSourceCandidate(
                        linkedSkillId,
                        LoadSummonSourceCandidateSkillMetadata(linkedSkillId),
                        linkedSkillNode);
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
            Func<int, SkillData> linkedSkillResolver = null,
            Func<int, IReadOnlyList<int>> reverseAffectedSkillResolver = null)
        {
            if (skill == null)
            {
                yield break;
            }

            var visitedSkillIds = new HashSet<int>();
            var pendingSkillIds = new Queue<int>();
            var enqueuedSkillIds = new HashSet<int>();

            if (skill.SkillId > 0)
            {
                visitedSkillIds.Add(skill.SkillId);
                enqueuedSkillIds.Add(skill.SkillId);
                pendingSkillIds.Enqueue(skill.SkillId);
            }

            foreach (int linkedSkillId in EnumerateDirectSummonSourceCandidateSkillIds(skill))
            {
                EnqueueSummonSourceCandidateSkillId(pendingSkillIds, enqueuedSkillIds, linkedSkillId);
            }

            foreach (int reverseParentSkillId in reverseAffectedSkillResolver?.Invoke(skill.SkillId) ?? Array.Empty<int>())
            {
                EnqueueSummonSourceCandidateSkillId(pendingSkillIds, enqueuedSkillIds, reverseParentSkillId);
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
                    if (!visitedSkillIds.Contains(nestedSkillId))
                    {
                        EnqueueSummonSourceCandidateSkillId(pendingSkillIds, enqueuedSkillIds, nestedSkillId);
                    }
                }

                foreach (int reverseParentSkillId in reverseAffectedSkillResolver?.Invoke(linkedSkillId) ?? Array.Empty<int>())
                {
                    if (!visitedSkillIds.Contains(reverseParentSkillId))
                    {
                        EnqueueSummonSourceCandidateSkillId(pendingSkillIds, enqueuedSkillIds, reverseParentSkillId);
                    }
                }
            }
        }

        private static void EnqueueSummonSourceCandidateSkillId(
            Queue<int> pendingSkillIds,
            HashSet<int> enqueuedSkillIds,
            int skillId)
        {
            if (skillId > 0 && enqueuedSkillIds.Add(skillId))
            {
                pendingSkillIds.Enqueue(skillId);
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

            if (skill.PassiveLinkedSkillIds != null)
            {
                foreach (int linkedSkillId in skill.PassiveLinkedSkillIds)
                {
                    if (linkedSkillId > 0)
                    {
                        yield return linkedSkillId;
                    }
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

        internal IReadOnlyList<int> FindAffectedSkillParentIds(int affectedSkillId)
        {
            if (affectedSkillId <= 0)
            {
                return Array.Empty<int>();
            }

            if (_affectedSkillParentCache.TryGetValue(affectedSkillId, out int[] cachedParentIds))
            {
                return cachedParentIds;
            }

            var parentSkillIds = new SortedSet<int>();
            foreach (int jobId in EnumerateAffectedSkillParentCandidateJobIds(affectedSkillId))
            {
                WzImage jobImage = GetSkillImage($"{jobId}.img");
                if (jobImage == null)
                {
                    continue;
                }

                jobImage.ParseImage();
                WzImageProperty skillRoot = jobImage["skill"];
                if (skillRoot?.WzProperties == null)
                {
                    continue;
                }

                foreach (WzImageProperty candidateSkillNode in skillRoot.WzProperties)
                {
                    if (candidateSkillNode == null
                        || !int.TryParse(candidateSkillNode.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parentSkillId)
                        || parentSkillId <= 0
                        || parentSkillId == affectedSkillId)
                    {
                        continue;
                    }

                    int[] affectedSkillIds = ParseLinkedSkillIds(candidateSkillNode["info"]?["affectedSkill"]);
                    if (Array.IndexOf(affectedSkillIds, affectedSkillId) >= 0)
                    {
                        parentSkillIds.Add(parentSkillId);
                    }
                }
            }

            int[] resolvedParentIds = parentSkillIds.ToArray();
            _affectedSkillParentCache[affectedSkillId] = resolvedParentIds;
            return resolvedParentIds;
        }

        internal IReadOnlyList<int> FindVisibleSummonSourceParentIds(int linkedSkillId)
        {
            if (linkedSkillId <= 0)
            {
                return Array.Empty<int>();
            }

            var parentSkillIds = new SortedSet<int>();
            AddParentSkillIds(parentSkillIds, FindAffectedSkillParentIds(linkedSkillId));
            AddParentSkillIds(parentSkillIds, FindPassiveSkillParentIds(linkedSkillId));
            AddParentSkillIds(parentSkillIds, FindDummySkillParentIds(linkedSkillId));
            AddParentSkillIds(parentSkillIds, FindRequiredSkillParentIds(linkedSkillId));

            return parentSkillIds.Count == 0
                ? Array.Empty<int>()
                : parentSkillIds.ToArray();
        }

        private static void AddParentSkillIds(SortedSet<int> parentSkillIds, IReadOnlyList<int> linkedParentSkillIds)
        {
            if (parentSkillIds == null || linkedParentSkillIds == null)
            {
                return;
            }

            foreach (int linkedParentSkillId in linkedParentSkillIds)
            {
                if (linkedParentSkillId > 0)
                {
                    parentSkillIds.Add(linkedParentSkillId);
                }
            }
        }

        internal IReadOnlyList<int> FindDummySkillParentIds(int dummySkillId)
        {
            if (dummySkillId <= 0)
            {
                return Array.Empty<int>();
            }

            if (_dummySkillParentCache.TryGetValue(dummySkillId, out int[] cachedParentIds))
            {
                return cachedParentIds;
            }

            var parentSkillIds = new SortedSet<int>();
            foreach (int jobId in EnumerateAffectedSkillParentCandidateJobIds(dummySkillId))
            {
                WzImage jobImage = GetSkillImage($"{jobId}.img");
                if (jobImage == null)
                {
                    continue;
                }

                jobImage.ParseImage();
                WzImageProperty skillRoot = jobImage["skill"];
                if (skillRoot?.WzProperties == null)
                {
                    continue;
                }

                foreach (WzImageProperty candidateSkillNode in skillRoot.WzProperties)
                {
                    if (candidateSkillNode == null
                        || !int.TryParse(candidateSkillNode.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parentSkillId)
                        || parentSkillId <= 0
                        || parentSkillId == dummySkillId)
                    {
                        continue;
                    }

                    int[] linkedDummySkillIds = ParseDummySkillParents(GetString(candidateSkillNode["info"], "dummyOf"));
                    if (Array.IndexOf(linkedDummySkillIds, dummySkillId) >= 0)
                    {
                        parentSkillIds.Add(parentSkillId);
                    }
                }
            }

            int[] resolvedParentIds = parentSkillIds.ToArray();
            _dummySkillParentCache[dummySkillId] = resolvedParentIds;
            return resolvedParentIds;
        }

        internal IReadOnlyList<int> FindPassiveSkillParentIds(int passiveLinkedSkillId)
        {
            if (passiveLinkedSkillId <= 0)
            {
                return Array.Empty<int>();
            }

            if (_passiveSkillParentCache.TryGetValue(passiveLinkedSkillId, out int[] cachedParentIds))
            {
                return cachedParentIds;
            }

            var parentSkillIds = new SortedSet<int>();
            foreach (int jobId in EnumerateAffectedSkillParentCandidateJobIds(passiveLinkedSkillId))
            {
                WzImage jobImage = GetSkillImage($"{jobId}.img");
                if (jobImage == null)
                {
                    continue;
                }

                jobImage.ParseImage();
                WzImageProperty skillRoot = jobImage["skill"];
                if (skillRoot?.WzProperties == null)
                {
                    continue;
                }

                foreach (WzImageProperty candidateSkillNode in skillRoot.WzProperties)
                {
                    if (candidateSkillNode == null
                        || !int.TryParse(candidateSkillNode.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parentSkillId)
                        || parentSkillId <= 0
                        || parentSkillId == passiveLinkedSkillId)
                    {
                        continue;
                    }

                    int[] linkedPassiveSkillIds = ParseLinkedSkillIds(candidateSkillNode["psdSkill"]);
                    if (Array.IndexOf(linkedPassiveSkillIds, passiveLinkedSkillId) >= 0)
                    {
                        parentSkillIds.Add(parentSkillId);
                    }
                }
            }

            int[] resolvedParentIds = parentSkillIds.ToArray();
            _passiveSkillParentCache[passiveLinkedSkillId] = resolvedParentIds;
            return resolvedParentIds;
        }

        internal IReadOnlyList<int> FindRequiredSkillParentIds(int requiredSkillId)
        {
            if (requiredSkillId <= 0)
            {
                return Array.Empty<int>();
            }

            if (_requiredSkillParentCache.TryGetValue(requiredSkillId, out int[] cachedParentIds))
            {
                return cachedParentIds;
            }

            var parentSkillIds = new SortedSet<int>();
            foreach (int jobId in EnumerateAffectedSkillParentCandidateJobIds(requiredSkillId))
            {
                WzImage jobImage = GetSkillImage($"{jobId}.img");
                if (jobImage == null)
                {
                    continue;
                }

                jobImage.ParseImage();
                WzImageProperty skillRoot = jobImage["skill"];
                if (skillRoot?.WzProperties == null)
                {
                    continue;
                }

                foreach (WzImageProperty candidateSkillNode in skillRoot.WzProperties)
                {
                    if (candidateSkillNode == null
                        || !int.TryParse(candidateSkillNode.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parentSkillId)
                        || parentSkillId <= 0
                        || parentSkillId == requiredSkillId)
                    {
                        continue;
                    }

                    int[] linkedRequiredSkillIds = ResolveRequiredSkillIds(candidateSkillNode, candidateSkillNode["info"]);
                    if (Array.IndexOf(linkedRequiredSkillIds, requiredSkillId) >= 0)
                    {
                        parentSkillIds.Add(parentSkillId);
                    }
                }
            }

            int[] resolvedParentIds = parentSkillIds.ToArray();
            _requiredSkillParentCache[requiredSkillId] = resolvedParentIds;
            return resolvedParentIds;
        }

        private IEnumerable<int> EnumerateAffectedSkillParentCandidateJobIds(int affectedSkillId)
        {
            int affectedJobId = affectedSkillId / 10000;
            IReadOnlyList<int> availableJobIds = EnumerateAvailableSkillBookJobIds();
            if (availableJobIds.Count == 0)
            {
                yield return affectedJobId;
                yield return affectedJobId + 1;
                yield break;
            }

            foreach (int jobId in availableJobIds)
            {
                if (IsAffectedSkillParentJobCandidate(affectedJobId, jobId))
                {
                    yield return jobId;
                }
            }
        }

        internal static bool IsAffectedSkillParentJobCandidate(int affectedJobId, int candidateJobId)
        {
            if (affectedJobId <= 0 || candidateJobId <= 0)
            {
                return false;
            }

            if (candidateJobId == affectedJobId)
            {
                return true;
            }

            return candidateJobId >= affectedJobId
                   && candidateJobId / 10 == affectedJobId / 10;
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

        internal static bool IsClientSummonOwnedEffectBranchName(string branchName)
        {
            return string.Equals(branchName, "effect", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "effect0", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "repeat", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "repeat0", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsClientSummonOwnedLoopingEffectBranchName(string branchName)
        {
            return string.Equals(branchName, "repeat", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "repeat0", StringComparison.OrdinalIgnoreCase);
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
            if (attackCount > 0)
            {
                if (!string.IsNullOrWhiteSpace(branchName))
                {
                    skill.SummonAttackCountOverridesByBranch[branchName] = attackCount;
                }

                if (skill.SummonAttackCountOverride <= 0)
                {
                    skill.SummonAttackCountOverride = attackCount;
                }
            }

            int mobCount = GetInt(infoNode, "mobCount");
            if (mobCount > 0)
            {
                if (!string.IsNullOrWhiteSpace(branchName))
                {
                    skill.SummonMobCountOverridesByBranch[branchName] = mobCount;
                }

                if (skill.SummonMobCountOverride <= 0)
                {
                    skill.SummonMobCountOverride = mobCount;
                }
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
                BuildSummonBranchProjectileAnimationPaths(
                    skill.SummonProjectileAnimationPaths,
                    LoadSummonIndexedAnimationPaths(attackBranch["info"]?["ball"])),
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

        private static List<string> BuildSummonBranchProjectileAnimationPaths(
            IEnumerable<string> basePaths,
            IEnumerable<string> overridePaths)
        {
            var resolved = new List<string>();
            AppendSummonIndexedAnimationPaths(resolved, basePaths);
            AppendSummonIndexedAnimationPaths(resolved, overridePaths);
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
            List<string> projectileAnimationPaths,
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

            if (projectileAnimationPaths?.Count > 0)
            {
                skill.SummonProjectileAnimationPathsByBranch[branchName] = projectileAnimationPaths;
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

        private static void AppendSummonIndexedAnimationPaths(List<string> destination, IEnumerable<string> source)
        {
            if (destination == null || source == null)
            {
                return;
            }

            foreach (string path in source)
            {
                if (string.IsNullOrWhiteSpace(path)
                    || destination.Any(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                destination.Add(path);
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
            AddLinkedSkillIds(linkedSkillIds, property);
            if (property.WzProperties != null)
            {
                foreach (WzImageProperty child in property.WzProperties)
                {
                    AddLinkedSkillIds(linkedSkillIds, child);
                }
            }

            return linkedSkillIds.Count == 0
                ? Array.Empty<int>()
                : linkedSkillIds.OrderBy(id => id).ToArray();
        }

        private static void AddLinkedSkillIds(HashSet<int> linkedSkillIds, WzImageProperty property)
        {
            if (linkedSkillIds == null || property == null)
            {
                return;
            }

            if (int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int namedSkillId)
                && namedSkillId > 0)
            {
                linkedSkillIds.Add(namedSkillId);
            }

            foreach (int valueSkillId in EnumerateLinkedSkillIdsFromPropertyValue(property))
            {
                linkedSkillIds.Add(valueSkillId);
            }
        }

        private static IEnumerable<int> EnumerateLinkedSkillIdsFromPropertyValue(WzImageProperty property)
        {
            switch (property)
            {
                case WzStringProperty stringProperty:
                    return ParseLinkedSkillIds(stringProperty.Value);
                case WzUOLProperty uolProperty:
                    if (!string.IsNullOrWhiteSpace(uolProperty.Value))
                    {
                        return ParseLinkedSkillIds(uolProperty.Value);
                    }

                    return ParseLinkedSkillIds(uolProperty.GetString());
                case WzIntProperty intProperty when intProperty.Value > 0:
                    return new[] { intProperty.Value };
                case WzShortProperty shortProperty when shortProperty.Value > 0:
                    return new[] { (int)shortProperty.Value };
                case WzLongProperty longProperty when longProperty.Value > 0:
                    return new[] { (int)longProperty.Value };
                default:
                    return Array.Empty<int>();
            }
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

        private bool TryResolveClientSummonedUolProperty(string summonedUolPath, out WzImageProperty summonNode)
        {
            summonNode = null;
            string normalizedPath = NormalizeClientSummonedUolPath(summonedUolPath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return false;
            }

            if (TryResolveSkillPropertyPath(normalizedPath, out WzImageProperty candidateNode))
            {
                if (LooksLikeSummonSourceProperty(candidateNode))
                {
                    summonNode = candidateNode;
                    return true;
                }

                if (TryResolveSummonSourcePropertyFromSkillNode(candidateNode, out summonNode))
                {
                    return true;
                }
            }

            if (TryResolveClientSummonedUolRootSkillNode(normalizedPath, out WzImageProperty rootSkillNode)
                && !ReferenceEquals(rootSkillNode, candidateNode)
                && TryResolveSummonSourcePropertyFromSkillNode(rootSkillNode, out summonNode))
            {
                return true;
            }

            foreach (int linkedSkillId in EnumerateClientSummonedUolLinkedSkillIds(normalizedPath, candidateNode))
            {
                if (TryGetSkillNode(linkedSkillId, out WzImageProperty linkedSkillNode)
                    && !ReferenceEquals(linkedSkillNode, rootSkillNode)
                    && TryResolveSummonSourcePropertyFromSkillNode(linkedSkillNode, out summonNode))
                {
                    return true;
                }
            }

            return TryResolveVisibleSummonSourcePropertyFromClientSummonedUolPath(normalizedPath, candidateNode, out summonNode);
        }

        private bool TryResolveClientSummonedUolRootSkillNode(string normalizedPath, out WzImageProperty rootSkillNode)
        {
            rootSkillNode = null;
            if (!TryParseClientSummonedUolRootSkillId(normalizedPath, out int rootSkillId))
            {
                return false;
            }

            return TryGetSkillNode(rootSkillId, out rootSkillNode);
        }

        private bool TryResolveVisibleSummonSourcePropertyFromClientSummonedUolPath(
            string summonedUolPath,
            WzImageProperty resolvedProperty,
            out WzImageProperty summonNode)
        {
            summonNode = null;
            foreach (int linkedSkillId in EnumerateVisibleSummonSourceCandidateSkillIdsFromClientSummonedUolPath(
                         summonedUolPath,
                         resolvedProperty,
                         LoadSummonSourceCandidateSkillMetadata,
                         FindVisibleSummonSourceParentIds))
            {
                if (!TryGetSkillNode(linkedSkillId, out WzImageProperty linkedSkillNode))
                {
                    continue;
                }

                if (TryResolveSummonSourcePropertyFromSkillNode(linkedSkillNode, out summonNode))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveSkillPropertyPath(string normalizedPath, out WzImageProperty property)
        {
            property = null;
            if (string.IsNullOrWhiteSpace(normalizedPath)
                || !normalizedPath.StartsWith("Skill/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] parts = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !parts[1].EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            WzImage image = GetSkillImage(parts[1]);
            if (image == null)
            {
                return false;
            }

            image.ParseImage();
            WzImageProperty current = image[parts[2]];
            for (int i = 3; current != null && i < parts.Length; i++)
            {
                current = current[parts[i]];
            }

            property = ResolveLinkedProperty(current);
            return property != null;
        }

        private static string ResolveClientSummonedUolPath(WzImageProperty skillNode, WzImageProperty infoNode)
        {
            return ResolveClientSummonedUolPaths(skillNode, infoNode).FirstOrDefault();
        }

        private static IReadOnlyList<string> ResolveClientSummonedUolPaths(WzImageProperty skillNode, WzImageProperty infoNode)
        {
            var normalizedPaths = new List<string>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ClientSummonedUolCandidateValue candidate in EnumerateClientSummonedUolCandidateValues(
                         skillNode,
                         infoNode,
                         ClientSummonedUolPropertyNames))
            {
                foreach (string normalizedPath in EnumerateNormalizedClientSummonedUolCandidatePaths(
                             candidate.Value,
                             candidate.ContextPathParts))
                {
                    if (!string.IsNullOrWhiteSpace(normalizedPath) && seenPaths.Add(normalizedPath))
                    {
                        normalizedPaths.Add(normalizedPath);
                    }
                }
            }

            foreach (string linkedRootPath in EnumerateClientSummonedUolHeuristicLinkedRootPaths(skillNode, infoNode))
            {
                if (!string.IsNullOrWhiteSpace(linkedRootPath) && seenPaths.Add(linkedRootPath))
                {
                    normalizedPaths.Add(linkedRootPath);
                }
            }

            string fallbackPath = BuildClientSummonedUolPathFromSkillNode(skillNode);
            if (!string.IsNullOrWhiteSpace(fallbackPath) && seenPaths.Add(fallbackPath))
            {
                normalizedPaths.Add(fallbackPath);
            }

            return normalizedPaths;
        }

        private static string ResolveClientTileUolPath(WzImageProperty skillNode, WzImageProperty infoNode)
        {
            return ResolveClientTileUolPathResolution(skillNode, infoNode).RootPath;
        }

        private static ClientSkillAssetUolPathResolution ResolveClientTileUolPathResolution(
            WzImageProperty skillNode,
            WzImageProperty infoNode)
        {
            return ResolveClientSkillAssetUolPathResolution(skillNode, infoNode, ClientTileUolPropertyNames);
        }

        internal static void PopulateClientTileUolFallbackFromZoneEffectForTest(SkillData skill)
        {
            PopulateClientTileUolFallbackFromZoneEffect(skill);
        }

        private static void PopulateClientTileUolFallbackFromZoneEffect(SkillData skill)
        {
            ZoneEffectData zoneEffect = skill?.ZoneEffect;
            if (zoneEffect == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(skill.ClientTileUolPath)
                && IsClientTileUolFallbackPath(zoneEffect.TileUolPath))
            {
                skill.ClientTileUolPath = zoneEffect.TileUolPath;
            }

            CopyClientTileUolFallbackPaths(
                zoneEffect.CharacterLevelTileUolPaths,
                skill.ClientCharacterLevelTileUolPaths);
            CopyClientTileUolFallbackPaths(
                zoneEffect.LevelTileUolPaths,
                skill.ClientLevelTileUolPaths);
        }

        private static void CopyClientTileUolFallbackPaths(
            IReadOnlyDictionary<int, string> source,
            IDictionary<int, string> target)
        {
            if (source == null || target == null)
            {
                return;
            }

            foreach (KeyValuePair<int, string> entry in source)
            {
                if (entry.Key <= 0
                    || target.ContainsKey(entry.Key)
                    || !IsClientTileUolFallbackPath(entry.Value))
                {
                    continue;
                }

                target[entry.Key] = entry.Value;
            }
        }

        private static bool IsClientTileUolFallbackPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string normalizedPath = path.Replace('\\', '/').Trim();
            return normalizedPath.Contains("/tile/", StringComparison.OrdinalIgnoreCase)
                   || normalizedPath.EndsWith("/tile", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedPath, "tile", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveClientBallUolPath(WzImageProperty skillNode, WzImageProperty infoNode, bool flip)
        {
            return ResolveClientBallUolPathResolution(skillNode, infoNode, flip).RootPath;
        }

        private static ClientSkillAssetUolPathResolution ResolveClientBallUolPathResolution(
            WzImageProperty skillNode,
            WzImageProperty infoNode,
            bool flip)
        {
            return ResolveClientSkillAssetUolPathResolution(
                skillNode,
                infoNode,
                flip ? ClientFlipBallUolPropertyNames : ClientBallUolPropertyNames);
        }

        private static ClientSkillAssetUolPathResolution ResolveClientSkillAssetUolPathResolution(
            WzImageProperty skillNode,
            WzImageProperty infoNode,
            IReadOnlyList<string> propertyNames)
        {
            string rootPath = null;
            var characterLevelPaths = new SortedDictionary<int, string>();
            var levelPaths = new Dictionary<int, string>();
            foreach (ClientSummonedUolCandidateValue candidate in EnumerateClientSummonedUolCandidateValues(
                         skillNode,
                         infoNode,
                         propertyNames))
            {
                string normalizedPath = NormalizeClientSummonedUolCandidatePath(candidate.Value, candidate.ContextPathParts);
                if (string.IsNullOrWhiteSpace(normalizedPath))
                {
                    continue;
                }

                if (TryResolveClientSkillAssetUolVariantLevel(
                        candidate.ContextPathParts,
                        out bool isCharacterLevelVariant,
                        out int variantLevel))
                {
                    IDictionary<int, string> target = isCharacterLevelVariant
                        ? characterLevelPaths
                        : levelPaths;
                    if (!target.ContainsKey(variantLevel))
                    {
                        target[variantLevel] = normalizedPath;
                    }

                    continue;
                }

                rootPath ??= normalizedPath;
            }

            return new ClientSkillAssetUolPathResolution(rootPath, characterLevelPaths, levelPaths);
        }

        private static bool TryResolveClientSkillAssetUolVariantLevel(
            string[] contextPathParts,
            out bool isCharacterLevelVariant,
            out int variantLevel)
        {
            isCharacterLevelVariant = false;
            variantLevel = 0;
            if (contextPathParts == null || contextPathParts.Length < 2)
            {
                return false;
            }

            for (int i = contextPathParts.Length - 2; i >= 0; i--)
            {
                string segment = contextPathParts[i]?.Trim();
                if (string.IsNullOrWhiteSpace(segment))
                {
                    continue;
                }

                bool characterLevel = segment.Equals("CharLevel", StringComparison.OrdinalIgnoreCase);
                bool level = segment.Equals("level", StringComparison.OrdinalIgnoreCase);
                if (!characterLevel && !level)
                {
                    continue;
                }

                if (!int.TryParse(contextPathParts[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedLevel)
                    || parsedLevel <= 0)
                {
                    continue;
                }

                isCharacterLevelVariant = characterLevel;
                variantLevel = parsedLevel;
                return true;
            }

            return false;
        }

        private static IEnumerable<ClientSummonedUolCandidateValue> EnumerateClientSummonedUolCandidateValues(
            WzImageProperty skillNode,
            WzImageProperty infoNode,
            IReadOnlyList<string> propertyNames)
        {
            if (propertyNames == null || propertyNames.Count == 0)
            {
                yield break;
            }

            foreach (WzImageProperty node in EnumerateClientSummonedUolCandidateNodes(skillNode, infoNode))
            {
                foreach (ClientSummonedUolCandidateValue candidate in EnumerateClientSummonedUolCandidateValuesForNode(
                             node,
                             skillNode,
                             propertyNames))
                {
                    yield return candidate;
                }
            }

            foreach (WzImageProperty node in EnumerateClientSummonedUolTableOnlyCandidateNodes(skillNode, infoNode))
            {
                foreach (string propertyName in propertyNames)
                {
                    foreach (ClientSummonedUolCandidateValue tableCandidate in EnumerateClientSummonedUolTableCandidateValues(
                                 node,
                                 skillNode,
                                 propertyName))
                    {
                        yield return tableCandidate;
                    }
                }
            }
        }

        private static IEnumerable<WzImageProperty> EnumerateClientSummonedUolCandidateNodes(
            WzImageProperty skillNode,
            WzImageProperty infoNode)
        {
            var yieldedNodePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WzImageProperty node in new[] { infoNode, skillNode })
            {
                if (TryAddClientSummonedUolCandidateNode(yieldedNodePaths, node))
                {
                    yield return node;
                }
            }

            foreach (WzImageProperty node in EnumerateClientSummonedUolSupplementalCandidateNodes(skillNode))
            {
                if (TryAddClientSummonedUolCandidateNode(yieldedNodePaths, node))
                {
                    yield return node;
                }
            }
        }

        private static IEnumerable<WzImageProperty> EnumerateClientSummonedUolSupplementalCandidateNodes(WzImageProperty skillNode)
        {
            if (skillNode == null)
            {
                yield break;
            }

            foreach (string branchName in new[] { "level", "CharLevel" })
            {
                WzImageProperty branchNode = skillNode[branchName];
                if (branchNode == null)
                {
                    continue;
                }

                yield return branchNode;
                if (branchNode.WzProperties == null)
                {
                    continue;
                }

                foreach (WzImageProperty child in branchNode.WzProperties)
                {
                    if (child == null)
                    {
                        continue;
                    }

                    yield return child;
                    WzImageProperty infoNode = child["info"];
                    if (infoNode != null)
                    {
                        yield return infoNode;
                    }
                }
            }
        }

        private static IEnumerable<WzImageProperty> EnumerateClientSummonedUolTableOnlyCandidateNodes(
            WzImageProperty skillNode,
            WzImageProperty infoNode)
        {
            if (skillNode == null)
            {
                yield break;
            }

            var yieldedNodePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WzImageProperty node in EnumerateClientSummonedUolCandidateNodes(skillNode, infoNode))
            {
                TryAddClientSummonedUolCandidateNode(yieldedNodePaths, node);
            }

            for (WzObject ancestor = skillNode.Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                if (ancestor is not WzImageProperty ancestorProperty)
                {
                    continue;
                }

                if (TryAddClientSummonedUolCandidateNode(yieldedNodePaths, ancestorProperty))
                {
                    yield return ancestorProperty;
                }

                foreach (string wrapperName in ClientSummonedUolTableWrapperNames)
                {
                    WzImageProperty wrapperNode = ancestorProperty[wrapperName];
                    if (TryAddClientSummonedUolCandidateNode(yieldedNodePaths, wrapperNode))
                    {
                        yield return wrapperNode;
                    }
                }
            }
        }

        private static bool TryAddClientSummonedUolCandidateNode(HashSet<string> yieldedNodePaths, WzImageProperty node)
        {
            if (node == null)
            {
                return false;
            }

            string normalizedPath = NormalizeClientSummonedUolFullPath(node.FullPath)
                                    ?? node.FullPath?.Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                normalizedPath = node.Name?.Trim();
            }

            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return true;
            }

            return yieldedNodePaths == null || yieldedNodePaths.Add(normalizedPath);
        }

        private static IEnumerable<ClientSummonedUolCandidateValue> EnumerateClientSummonedUolCandidateValuesForNode(
            WzImageProperty node,
            WzImageProperty skillNode,
            IReadOnlyList<string> propertyNames)
        {
            if (node == null)
            {
                yield break;
            }

            foreach (string propertyName in propertyNames)
            {
                string value = GetClientSummonedUolCandidateValue(node, propertyName);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return new ClientSummonedUolCandidateValue(
                        value,
                        BuildClientSummonedUolCandidateContextPathParts(node, propertyName, skillNode));
                }

                foreach (ClientSummonedUolCandidateValue tableCandidate in EnumerateClientSummonedUolTableCandidateValues(
                             node,
                             skillNode,
                             propertyName))
                {
                    yield return tableCandidate;
                }
            }

            foreach (ClientSummonedUolCandidateValue heuristicCandidate in EnumerateClientSummonedUolHeuristicCandidateValues(node, skillNode))
            {
                yield return heuristicCandidate;
            }
        }

        private static IEnumerable<ClientSummonedUolCandidateValue> EnumerateClientSummonedUolTableCandidateValues(
            WzImageProperty node,
            WzImageProperty skillNode,
            string propertyName)
        {
            if (node == null
                || string.IsNullOrWhiteSpace(propertyName)
                || !TryParseRequiredSkillId(skillNode?.Name, out int skillId))
            {
                yield break;
            }

            WzImageProperty tableNode = node[propertyName];
            foreach (ClientSummonedUolCandidateValue recordCandidate in EnumerateClientSummonedUolTableRecordCandidateValues(
                         tableNode,
                         node,
                         skillNode,
                         propertyName,
                         skillId))
            {
                yield return recordCandidate;
            }

            foreach (WzImageProperty tableEntry in EnumerateClientSummonedUolTableEntryNodes(tableNode, skillId))
            {
                string entryRelativePath = BuildClientSummonedUolTableEntryRelativePath(
                    propertyName,
                    tableNode,
                    tableEntry,
                    skillId);
                string[] entryPathParts = BuildClientSummonedUolCandidateContextPathParts(
                    node,
                    entryRelativePath,
                    skillNode);
                string value = GetClientSummonedUolCandidateValue(tableEntry);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return new ClientSummonedUolCandidateValue(value, entryPathParts);
                }

                foreach ((WzImageProperty Property, string RelativePath, bool UseNameAsValue) tableValue in EnumerateClientSummonedUolTableEntryValues(
                             tableEntry,
                             relativePathPrefix: string.Empty,
                             depthRemaining: ClientSummonedUolTableEntryTraversalDepth))
                {
                    value = tableValue.UseNameAsValue
                        ? tableValue.Property?.Name
                        : GetClientSummonedUolCandidateValue(tableValue.Property);
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    yield return new ClientSummonedUolCandidateValue(
                        value,
                        BuildResolvedClientSummonedUolNestedPathParts(entryPathParts, tableValue.RelativePath));
                }
            }
        }

        private static IEnumerable<ClientSummonedUolCandidateValue> EnumerateClientSummonedUolTableRecordCandidateValues(
            WzImageProperty tableNode,
            WzImageProperty contextNode,
            WzImageProperty skillNode,
            string propertyName,
            int skillId)
        {
            if (tableNode?.WzProperties == null || skillId <= 0)
            {
                yield break;
            }

            foreach ((WzImageProperty Property, string RelativePath) recordLeaf in EnumerateClientSummonedUolTableRecordLeaves(
                         tableNode,
                         relativePathPrefix: string.Empty,
                         depthRemaining: ClientSummonedUolTableEntryTraversalDepth))
            {
                string value = GetClientSummonedUolCandidateValue(recordLeaf.Property);
                if (string.IsNullOrWhiteSpace(value)
                    || !ClientSummonedUolTableRecordReferencesSkill(value, skillId))
                {
                    continue;
                }

                string relativePath = string.IsNullOrWhiteSpace(recordLeaf.RelativePath)
                    ? propertyName
                    : $"{propertyName}/{recordLeaf.RelativePath}";
                yield return new ClientSummonedUolCandidateValue(
                    value,
                    BuildClientSummonedUolCandidateContextPathParts(contextNode, relativePath, skillNode));
            }
        }

        private static IEnumerable<(WzImageProperty Property, string RelativePath)> EnumerateClientSummonedUolTableRecordLeaves(
            WzImageProperty node,
            string relativePathPrefix,
            int depthRemaining)
        {
            if (node?.WzProperties == null || depthRemaining <= 0)
            {
                yield break;
            }

            foreach (WzImageProperty child in node.WzProperties)
            {
                if (child == null || string.IsNullOrWhiteSpace(child.Name))
                {
                    continue;
                }

                string relativePath = string.IsNullOrWhiteSpace(relativePathPrefix)
                    ? child.Name
                    : $"{relativePathPrefix}/{child.Name}";
                if (IsClientSummonedUolCandidateValueProperty(child)
                    && !IsClientSummonedUolTableEntryValueName(child.Name)
                    && !TryParseRequiredSkillId(child.Name, out _))
                {
                    yield return (child, relativePath);
                }

                foreach ((WzImageProperty Property, string RelativePath) nestedLeaf in EnumerateClientSummonedUolTableRecordLeaves(
                             child,
                             relativePath,
                             depthRemaining - 1))
                {
                    yield return nestedLeaf;
                }
            }
        }

        private static bool ClientSummonedUolTableRecordReferencesSkill(string value, int skillId)
        {
            if (string.IsNullOrWhiteSpace(value) || skillId <= 0)
            {
                return false;
            }

            string skillIdText = skillId.ToString(CultureInfo.InvariantCulture);
            return ContainsClientSummonedUolTableSkillIdToken(value, skillIdText);
        }

        private static IEnumerable<(WzImageProperty Property, string RelativePath, bool UseNameAsValue)> EnumerateClientSummonedUolTableEntryValues(
            WzImageProperty tableEntry,
            string relativePathPrefix,
            int depthRemaining)
        {
            if (tableEntry?.WzProperties == null || depthRemaining <= 0)
            {
                yield break;
            }

            foreach (WzImageProperty child in tableEntry.WzProperties)
            {
                if (child == null || string.IsNullOrWhiteSpace(child.Name))
                {
                    continue;
                }

                string relativePath = string.IsNullOrWhiteSpace(relativePathPrefix)
                    ? child.Name
                    : $"{relativePathPrefix}/{child.Name}";
                if (IsClientSummonedUolTableEntryValueName(child.Name))
                {
                    yield return (child, relativePath, UseNameAsValue: false);
                }

                if (LooksLikeClientSummonedUolRelativePathToken(child.Name)
                    && !TryParseRequiredSkillId(child.Name, out _))
                {
                    yield return (child, relativePath, UseNameAsValue: true);
                }

                foreach ((WzImageProperty Property, string RelativePath, bool UseNameAsValue) nestedValue in EnumerateClientSummonedUolTableEntryValues(
                             child,
                             relativePath,
                             depthRemaining - 1))
                {
                    yield return nestedValue;
                }
            }
        }

        private static IEnumerable<WzImageProperty> EnumerateClientSummonedUolTableEntryNodes(
            WzImageProperty tableNode,
            int skillId)
        {
            if (tableNode == null || skillId <= 0)
            {
                yield break;
            }

            string skillIdText = skillId.ToString(CultureInfo.InvariantCulture);
            var yieldedNodePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WzImageProperty entryNode in EnumerateClientSummonedUolTableEntryNodeCandidates(tableNode, skillId, skillIdText))
            {
                if (TryAddClientSummonedUolCandidateNode(yieldedNodePaths, entryNode))
                {
                    yield return entryNode;
                }
            }
        }

        private static IEnumerable<WzImageProperty> EnumerateClientSummonedUolTableEntryNodeCandidates(
            WzImageProperty tableNode,
            int skillId,
            string skillIdText)
        {
            if (TryReadClientSummonedUolTableOwnerSkillId(tableNode, out int tableOwnerSkillId)
                && tableOwnerSkillId == skillId)
            {
                yield return tableNode;
            }

            WzImageProperty directEntry = tableNode[skillIdText];
            if (directEntry != null)
            {
                yield return directEntry;
            }

            int jobId = skillId / 10000;
            string jobIdText = jobId.ToString(CultureInfo.InvariantCulture);
            string imageName = $"{jobIdText}.img";
            foreach (string groupName in new[] { jobIdText, imageName })
            {
                WzImageProperty groupedEntry = tableNode[groupName]?[skillIdText];
                if (groupedEntry != null)
                {
                    yield return groupedEntry;
                }
            }

            WzImageProperty mountedSkillEntry = tableNode["Skill"]?[imageName]?["skill"]?[skillIdText];
            if (mountedSkillEntry != null)
            {
                yield return mountedSkillEntry;
            }

            WzImageProperty archiveMountedSkillEntry = tableNode["Skill.wz"]?["Skill"]?[imageName]?["skill"]?[skillIdText];
            if (archiveMountedSkillEntry != null)
            {
                yield return archiveMountedSkillEntry;
            }

            WzImageProperty archiveRootMountedSkillEntry = tableNode["Skill.wz"]?[imageName]?["skill"]?[skillIdText];
            if (archiveRootMountedSkillEntry != null)
            {
                yield return archiveRootMountedSkillEntry;
            }

            WzImageProperty mountedSkillEntryWithoutImageSuffix = tableNode["Skill"]?[jobIdText]?["skill"]?[skillIdText];
            if (mountedSkillEntryWithoutImageSuffix != null)
            {
                yield return mountedSkillEntryWithoutImageSuffix;
            }

            WzImageProperty archiveMountedSkillEntryWithoutImageSuffix = tableNode["Skill.wz"]?["Skill"]?[jobIdText]?["skill"]?[skillIdText];
            if (archiveMountedSkillEntryWithoutImageSuffix != null)
            {
                yield return archiveMountedSkillEntryWithoutImageSuffix;
            }

            WzImageProperty imageGroupedEntry = tableNode[imageName]?[skillIdText];
            if (imageGroupedEntry != null)
            {
                yield return imageGroupedEntry;
            }

            WzImageProperty imageMountedSkillEntry = tableNode[imageName]?["skill"]?[skillIdText];
            if (imageMountedSkillEntry != null)
            {
                yield return imageMountedSkillEntry;
            }

            WzImageProperty jobMountedSkillEntry = tableNode[jobIdText]?["skill"]?[skillIdText];
            if (jobMountedSkillEntry != null)
            {
                yield return jobMountedSkillEntry;
            }

            WzImageProperty mountedImageGroupedEntry = tableNode["Skill"]?[imageName]?[skillIdText];
            if (mountedImageGroupedEntry != null)
            {
                yield return mountedImageGroupedEntry;
            }

            WzImageProperty wzMountedSkillEntry = tableNode["wz"]?["Skill"]?[imageName]?["skill"]?[skillIdText];
            if (wzMountedSkillEntry != null)
            {
                yield return wzMountedSkillEntry;
            }

            foreach (WzImageProperty tokenNamedEntry in EnumerateClientSummonedUolTableSkillTokenNamedEntries(
                         tableNode,
                         skillId))
            {
                yield return tokenNamedEntry;
            }

            foreach (WzImageProperty ownerMatchedEntry in EnumerateClientSummonedUolTableOwnerFieldEntryNodes(
                         tableNode,
                         skillId,
                         depthRemaining: ClientSummonedUolTableOwnerFieldTraversalDepth))
            {
                yield return ownerMatchedEntry;
            }
        }

        private static IEnumerable<WzImageProperty> EnumerateClientSummonedUolTableOwnerFieldEntryNodes(
            WzImageProperty node,
            int skillId,
            int depthRemaining)
        {
            if (node?.WzProperties == null || skillId <= 0 || depthRemaining < 0)
            {
                yield break;
            }

            foreach (WzImageProperty child in node.WzProperties)
            {
                if (child?.WzProperties == null || string.IsNullOrWhiteSpace(child.Name))
                {
                    continue;
                }

                if (TryReadClientSummonedUolTableOwnerSkillId(child, out int ownerSkillId))
                {
                    if (ownerSkillId == skillId)
                    {
                        yield return child;
                    }

                    continue;
                }

                if (LooksLikeClientSummonedUolTableRowContainer(child)
                    && TryReadNestedClientSummonedUolTableOwnerSkillId(
                        child,
                        out ownerSkillId,
                        depthRemaining: ClientSummonedUolTableOwnerFieldTraversalDepth))
                {
                    if (ownerSkillId == skillId)
                    {
                        yield return child;
                    }

                    continue;
                }

                foreach (WzImageProperty nestedEntry in EnumerateClientSummonedUolTableOwnerFieldEntryNodes(
                             child,
                             skillId,
                             depthRemaining - 1))
                {
                    yield return nestedEntry;
                }
            }
        }

        private static IEnumerable<WzImageProperty> EnumerateClientSummonedUolTableSkillTokenNamedEntries(
            WzImageProperty tableNode,
            int skillId)
        {
            if (tableNode?.WzProperties == null || skillId <= 0)
            {
                yield break;
            }

            string skillIdText = skillId.ToString(CultureInfo.InvariantCulture);
            foreach (WzImageProperty child in tableNode.WzProperties)
            {
                if (child == null
                    || string.IsNullOrWhiteSpace(child.Name)
                    || child.Name.Equals(skillIdText, StringComparison.OrdinalIgnoreCase)
                    || !ContainsClientSummonedUolTableSkillIdToken(child.Name, skillIdText))
                {
                    continue;
                }

                yield return child;
            }
        }

        private static bool ContainsClientSummonedUolTableSkillIdToken(string name, string skillIdText)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(skillIdText))
            {
                return false;
            }

            int searchIndex = 0;
            while (searchIndex < name.Length)
            {
                int matchIndex = name.IndexOf(skillIdText, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (matchIndex < 0)
                {
                    return false;
                }

                int beforeIndex = matchIndex - 1;
                int afterIndex = matchIndex + skillIdText.Length;
                bool separatedBefore = beforeIndex < 0 || !char.IsDigit(name[beforeIndex]);
                bool separatedAfter = afterIndex >= name.Length || !char.IsDigit(name[afterIndex]);
                if (separatedBefore && separatedAfter)
                {
                    return true;
                }

                searchIndex = matchIndex + skillIdText.Length;
            }

            return false;
        }

        private static bool TryReadClientSummonedUolTableOwnerSkillId(WzImageProperty rowNode, out int skillId)
        {
            skillId = 0;
            if (rowNode?.WzProperties == null)
            {
                return false;
            }

            foreach (WzImageProperty child in rowNode.WzProperties)
            {
                if (child == null)
                {
                    continue;
                }

                if (TryReadClientSummonedUolTableOwnerSkillIdFromFieldName(child.Name, out skillId))
                {
                    return true;
                }

                if (!IsClientSummonedUolTableOwnerFieldName(child.Name))
                {
                    continue;
                }

                string value = GetClientSummonedUolCandidateValue(child);
                if (!string.IsNullOrWhiteSpace(value)
                    && int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSkillId)
                    && parsedSkillId > 0)
                {
                    skillId = parsedSkillId;
                    return true;
                }

                foreach (int linkedSkillId in ParseLinkedSkillIds(value))
                {
                    if (LooksLikeClientSummonedUolInferredSkillId(linkedSkillId))
                    {
                        skillId = linkedSkillId;
                        return true;
                    }
                }

                foreach (int fallbackSkillId in EnumerateClientSummonedUolFallbackSkillIdsFromValue(value, contextSkillId: 0))
                {
                    if (LooksLikeClientSummonedUolInferredSkillId(fallbackSkillId))
                    {
                        skillId = fallbackSkillId;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryReadNestedClientSummonedUolTableOwnerSkillId(
            WzImageProperty rowNode,
            out int skillId,
            int depthRemaining)
        {
            skillId = 0;
            if (rowNode?.WzProperties == null || depthRemaining <= 0)
            {
                return false;
            }

            foreach (WzImageProperty child in rowNode.WzProperties)
            {
                if (child == null)
                {
                    continue;
                }

                if (IsClientSummonedUolTableOwnerFieldName(child.Name))
                {
                    string value = GetClientSummonedUolCandidateValue(child);
                    if (!string.IsNullOrWhiteSpace(value)
                        && int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSkillId)
                        && parsedSkillId > 0)
                    {
                        skillId = parsedSkillId;
                        return true;
                    }

                    foreach (int linkedSkillId in ParseLinkedSkillIds(value))
                    {
                        if (LooksLikeClientSummonedUolInferredSkillId(linkedSkillId))
                        {
                            skillId = linkedSkillId;
                            return true;
                        }
                    }

                    foreach (int fallbackSkillId in EnumerateClientSummonedUolFallbackSkillIdsFromValue(value, contextSkillId: 0))
                    {
                        if (LooksLikeClientSummonedUolInferredSkillId(fallbackSkillId))
                        {
                            skillId = fallbackSkillId;
                            return true;
                        }
                    }
                }
                else if (TryReadClientSummonedUolTableOwnerSkillIdFromFieldName(child.Name, out skillId))
                {
                    return true;
                }

                if (child.WzProperties == null)
                {
                    continue;
                }

                if (TryReadNestedClientSummonedUolTableOwnerSkillId(child, out skillId, depthRemaining - 1))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeClientSummonedUolTableRowContainer(WzImageProperty rowNode)
        {
            if (rowNode == null || string.IsNullOrWhiteSpace(rowNode.Name))
            {
                return false;
            }

            if (int.TryParse(rowNode.Name.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return true;
            }

            string normalizedName = NormalizeClientSummonedUolHeuristicPathSegment(rowNode.Name);
            return normalizedName.StartsWith("row", StringComparison.Ordinal)
                   || normalizedName.StartsWith("entry", StringComparison.Ordinal)
                   || normalizedName.StartsWith("record", StringComparison.Ordinal);
        }

        private static bool IsClientSummonedUolTableOwnerFieldName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string normalizedName = NormalizeClientSummonedUolHeuristicPathSegment(name);
            return ClientSummonedUolTableOwnerFieldNames.Contains(normalizedName, StringComparer.Ordinal);
        }

        private static bool TryReadClientSummonedUolTableOwnerSkillIdFromFieldName(string name, out int skillId)
        {
            skillId = 0;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (!TryReadClientSummonedUolTableEncodedOwnerFieldNamePrefix(name, out string normalizedName))
            {
                return false;
            }

            foreach (string ownerFieldName in ClientSummonedUolTableOwnerFieldNames)
            {
                if (!normalizedName.StartsWith(ownerFieldName, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (int linkedSkillId in ParseLinkedSkillIds(name))
                {
                    if (LooksLikeClientSummonedUolInferredSkillId(linkedSkillId))
                    {
                        skillId = linkedSkillId;
                        return true;
                    }
                }

                foreach (int fallbackSkillId in EnumerateClientSummonedUolFallbackSkillIdsFromValue(name, contextSkillId: 0))
                {
                    if (LooksLikeClientSummonedUolInferredSkillId(fallbackSkillId))
                    {
                        skillId = fallbackSkillId;
                        return true;
                    }
                }

                return false;
            }

            return false;
        }

        private static bool TryReadClientSummonedUolTableEncodedOwnerFieldNamePrefix(
            string name,
            out string normalizedName)
        {
            normalizedName = string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            int prefixLength = 0;
            while (prefixLength < name.Length && char.IsLetterOrDigit(name[prefixLength]))
            {
                prefixLength++;
            }

            if (prefixLength <= 0 || prefixLength >= name.Length)
            {
                return false;
            }

            normalizedName = NormalizeClientSummonedUolHeuristicPathSegment(name.Substring(0, prefixLength));
            return !string.IsNullOrWhiteSpace(normalizedName);
        }

        private static string BuildClientSummonedUolTableEntryRelativePath(
            string propertyName,
            WzImageProperty tableNode,
            WzImageProperty tableEntry,
            int skillId)
        {
            string skillIdText = skillId.ToString(CultureInfo.InvariantCulture);
            string tablePath = NormalizeClientSummonedUolFullPath(tableNode?.FullPath)
                               ?? tableNode?.FullPath?.Trim().Replace('\\', '/');
            string entryPath = NormalizeClientSummonedUolFullPath(tableEntry?.FullPath)
                               ?? tableEntry?.FullPath?.Trim().Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(tablePath)
                && !string.IsNullOrWhiteSpace(entryPath)
                && entryPath.StartsWith(tablePath, StringComparison.OrdinalIgnoreCase))
            {
                string suffix = entryPath[tablePath.Length..].TrimStart('/');
                if (!string.IsNullOrWhiteSpace(suffix))
                {
                    return $"{propertyName}/{suffix}";
                }
            }

            return $"{propertyName}/{skillIdText}";
        }

        private static bool IsClientSummonedUolTableEntryValueName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return name.Equals("0", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("path", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("uol", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("uolPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("tilePath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("tileUolPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("tileUOLPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("ballPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("ballUolPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("ballUOLPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("flipBallPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("flipBallUolPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("flipBallUOLPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("summon", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("summonPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("summonUolPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("summonUOLPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("summonedPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("summonedUolPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("summonedUOLPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("summoned", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("summonedValue", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("target", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("targetPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("targetUol", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("targetUolPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("targetUOLPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("sourceUol", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("sourceUolPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("sourceUOLPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("sourcePath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("srcPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("asset", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("assetPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("clientPath", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("uolValue", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("uolTarget", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("value", StringComparison.OrdinalIgnoreCase)
                   || ClientSummonedUolPropertyNames.Contains(name, StringComparer.OrdinalIgnoreCase)
                   || ClientTileUolPropertyNames.Contains(name, StringComparer.OrdinalIgnoreCase)
                   || ClientBallUolPropertyNames.Contains(name, StringComparer.OrdinalIgnoreCase)
                   || ClientFlipBallUolPropertyNames.Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<ClientSummonedUolCandidateValue> EnumerateClientSummonedUolHeuristicCandidateValues(
            WzImageProperty node,
            WzImageProperty skillNode)
        {
            if (node == null)
            {
                yield break;
            }

            foreach ((WzImageProperty Property, string RelativePath) in EnumerateClientSummonedUolHeuristicCandidateProperties(node))
            {
                string value = GetClientSummonedUolCandidateValue(Property);
                if (string.IsNullOrWhiteSpace(value)
                    || !LooksLikeClientSummonedUolHeuristicCandidateValue(value, RelativePath))
                {
                    continue;
                }

                yield return new ClientSummonedUolCandidateValue(
                    value,
                    BuildClientSummonedUolCandidateContextPathParts(node, RelativePath, skillNode));
            }
        }

        private static IEnumerable<(WzImageProperty Property, string RelativePath)> EnumerateClientSummonedUolHeuristicCandidateProperties(
            WzImageProperty node)
        {
            if (node?.WzProperties == null)
            {
                yield break;
            }

            string normalizedNodeName = node.Name?.Trim() ?? string.Empty;
            bool nodeIsInfoBranch = normalizedNodeName.Equals("info", StringComparison.OrdinalIgnoreCase);
            if (nodeIsInfoBranch)
            {
                foreach ((WzImageProperty Property, string RelativePath) nestedInfoLeaf in EnumerateClientSummonedUolHeuristicInfoLeaves(
                             node,
                             relativePathPrefix: normalizedNodeName,
                             depthRemaining: ClientSummonedUolHeuristicInfoTraversalDepth))
                {
                    yield return nestedInfoLeaf;
                }
            }

            foreach (WzImageProperty child in node.WzProperties)
            {
                if (child == null)
                {
                    continue;
                }

                if (IsClientSummonedUolCandidateValueProperty(child))
                {
                    yield return (child, child.Name);
                }

                if (!child.Name.Equals("info", StringComparison.OrdinalIgnoreCase))
                {
                    foreach ((WzImageProperty Property, string RelativePath) nestedSkillLeaf in EnumerateClientSummonedUolHeuristicSkillLeaves(
                                 child,
                                 relativePathPrefix: child.Name,
                                 depthRemaining: ClientSummonedUolHeuristicSkillTraversalDepth))
                    {
                        yield return nestedSkillLeaf;
                    }

                    continue;
                }

                foreach ((WzImageProperty Property, string RelativePath) nestedInfoLeaf in EnumerateClientSummonedUolHeuristicInfoLeaves(
                             child,
                             relativePathPrefix: child.Name,
                             depthRemaining: ClientSummonedUolHeuristicInfoTraversalDepth))
                {
                    yield return nestedInfoLeaf;
                }
            }
        }

        private static IEnumerable<(WzImageProperty Property, string RelativePath)> EnumerateClientSummonedUolHeuristicSkillLeaves(
            WzImageProperty node,
            string relativePathPrefix,
            int depthRemaining)
        {
            if (node?.WzProperties == null || depthRemaining <= 0)
            {
                yield break;
            }

            foreach (WzImageProperty child in node.WzProperties)
            {
                if (child == null || string.IsNullOrWhiteSpace(child.Name))
                {
                    continue;
                }

                string relativePath = string.IsNullOrWhiteSpace(relativePathPrefix)
                    ? child.Name
                    : $"{relativePathPrefix}/{child.Name}";
                if (IsClientSummonedUolCandidateValueProperty(child))
                {
                    yield return (child, relativePath);
                }

                foreach ((WzImageProperty Property, string RelativePath) nestedLeaf in EnumerateClientSummonedUolHeuristicSkillLeaves(
                             child,
                             relativePath,
                             depthRemaining - 1))
                {
                    yield return nestedLeaf;
                }
            }
        }

        private static IEnumerable<(WzImageProperty Property, string RelativePath)> EnumerateClientSummonedUolHeuristicInfoLeaves(
            WzImageProperty node,
            string relativePathPrefix,
            int depthRemaining)
        {
            if (node?.WzProperties == null || depthRemaining < 0)
            {
                yield break;
            }

            foreach (WzImageProperty child in node.WzProperties)
            {
                if (child == null || string.IsNullOrWhiteSpace(child.Name))
                {
                    continue;
                }

                string relativePath = string.IsNullOrWhiteSpace(relativePathPrefix)
                    ? child.Name
                    : $"{relativePathPrefix}/{child.Name}";
                if (IsClientSummonedUolCandidateValueProperty(child))
                {
                    yield return (child, relativePath);
                }

                foreach ((WzImageProperty Property, string RelativePath) nestedLeaf in EnumerateClientSummonedUolHeuristicInfoLeaves(
                             child,
                             relativePath,
                             depthRemaining - 1))
                {
                    yield return nestedLeaf;
                }
            }
        }

        private static bool IsClientSummonedUolCandidateValueProperty(WzImageProperty property)
        {
            return property is WzStringProperty
                   || property is WzUOLProperty
                   || property is WzIntProperty
                   || property is WzShortProperty
                   || property is WzLongProperty
                   || property is WzFloatProperty
                   || property is WzDoubleProperty;
        }

        private static bool LooksLikeClientSummonedUolHeuristicCandidateValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.IndexOf("summon", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return value.IndexOf("/skill/", StringComparison.OrdinalIgnoreCase) >= 0
                   && value.IndexOf(".img", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeClientSummonedUolHeuristicCandidateValue(string value, string relativePath)
        {
            if (LooksLikeClientSummonedUolHeuristicCandidateValue(value))
            {
                return true;
            }

            return LooksLikeClientSummonedUolHeuristicOwnerPath(relativePath)
                   && LooksLikeClientSummonedUolHeuristicOwnerValue(value);
        }

        private static bool LooksLikeClientSummonedUolHeuristicOwnerPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            foreach (string segment in relativePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string normalizedSegment = NormalizeClientSummonedUolHeuristicPathSegment(segment);
                if (string.IsNullOrWhiteSpace(normalizedSegment))
                {
                    continue;
                }

                if (normalizedSegment.Contains("summon", StringComparison.Ordinal)
                    || normalizedSegment.Contains("uol", StringComparison.Ordinal)
                    || normalizedSegment.Contains("owner", StringComparison.Ordinal)
                    || normalizedSegment.Contains("requireskill", StringComparison.Ordinal)
                    || normalizedSegment.Contains("affectedskill", StringComparison.Ordinal)
                    || normalizedSegment.Contains("dummyof", StringComparison.Ordinal)
                    || normalizedSegment.Contains("psdskill", StringComparison.Ordinal)
                    || normalizedSegment.Equals("req", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeClientSummonedUolHeuristicPathSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return string.Empty;
            }

            var normalizedChars = new char[segment.Length];
            int normalizedLength = 0;
            foreach (char ch in segment)
            {
                if (!char.IsLetterOrDigit(ch))
                {
                    continue;
                }

                normalizedChars[normalizedLength++] = char.ToLowerInvariant(ch);
            }

            return normalizedLength <= 0
                ? string.Empty
                : new string(normalizedChars, 0, normalizedLength);
        }

        private static bool LooksLikeClientSummonedUolHeuristicOwnerValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (int parsedLinkedSkillId in ParseLinkedSkillIds(value))
            {
                if (LooksLikeClientSummonedUolInferredSkillId(parsedLinkedSkillId))
                {
                    return true;
                }
            }

            foreach (int fallbackSkillId in EnumerateClientSummonedUolFallbackSkillIdsFromValue(value, contextSkillId: 0))
            {
                if (LooksLikeClientSummonedUolInferredSkillId(fallbackSkillId))
                {
                    return true;
                }
            }

            foreach (string token in EnumerateClientSummonedUolPathTokensFromValue(value))
            {
                if (LooksLikeClientSummonedUolRelativePathToken(token))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateClientSummonedUolHeuristicLinkedRootPaths(
            WzImageProperty skillNode,
            WzImageProperty infoNode)
        {
            var yieldedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WzImageProperty node in EnumerateClientSummonedUolCandidateNodes(skillNode, infoNode))
            {
                foreach (int linkedSkillId in EnumerateClientSummonedUolHeuristicLinkedSkillIds(node, skillNode))
                {
                    if (!LooksLikeClientSummonedUolInferredSkillId(linkedSkillId))
                    {
                        continue;
                    }

                    string linkedRootPath = $"Skill/{linkedSkillId / 10000}.img/skill/{linkedSkillId}";
                    if (yieldedPaths.Add(linkedRootPath))
                    {
                        yield return linkedRootPath;
                    }
                }
            }
        }

        private static IEnumerable<int> EnumerateClientSummonedUolHeuristicLinkedSkillIds(
            WzImageProperty node,
            WzImageProperty skillNode)
        {
            int contextSkillId = TryParseRequiredSkillId(skillNode?.Name, out int parsedSkillId)
                ? parsedSkillId
                : 0;
            var yieldedSkillIds = new HashSet<int>();
            foreach ((WzImageProperty Branch, string RelativePath) in EnumerateClientSummonedUolHeuristicCandidateBranches(node))
            {
                if (!LooksLikeClientSummonedUolHeuristicOwnerPath(RelativePath))
                {
                    continue;
                }

                if (TryParseRequiredSkillId(Branch.Name, out int branchSkillId))
                {
                    foreach (int candidateSkillId in ExpandClientSummonedUolHeuristicLinkedSkillIdCandidates(
                                 branchSkillId,
                                 contextSkillId))
                    {
                        if (yieldedSkillIds.Add(candidateSkillId))
                        {
                            yield return candidateSkillId;
                        }
                    }
                }

                foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromChildNames(Branch))
                {
                    foreach (int candidateSkillId in ExpandClientSummonedUolHeuristicLinkedSkillIdCandidates(
                                 linkedSkillId,
                                 contextSkillId))
                    {
                        if (yieldedSkillIds.Add(candidateSkillId))
                        {
                            yield return candidateSkillId;
                        }
                    }
                }

                foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromChildNameTokens(Branch))
                {
                    foreach (int candidateSkillId in ExpandClientSummonedUolHeuristicLinkedSkillIdCandidates(
                                 linkedSkillId,
                                 contextSkillId))
                    {
                        if (yieldedSkillIds.Add(candidateSkillId))
                        {
                            yield return candidateSkillId;
                        }
                    }
                }
            }
        }

        private static IEnumerable<int> ExpandClientSummonedUolHeuristicLinkedSkillIdCandidates(
            int linkedSkillId,
            int contextSkillId)
        {
            if (linkedSkillId <= 0)
            {
                yield break;
            }

            yield return linkedSkillId;
            if (TryBuildClientSummonedUolContextualSkillId(contextSkillId, linkedSkillId, out int contextualSkillId))
            {
                yield return contextualSkillId;
            }
        }

        private static IEnumerable<(WzImageProperty Branch, string RelativePath)> EnumerateClientSummonedUolHeuristicCandidateBranches(
            WzImageProperty node)
        {
            if (node?.WzProperties == null)
            {
                yield break;
            }

            string normalizedNodeName = node.Name?.Trim() ?? string.Empty;
            bool nodeIsInfoBranch = normalizedNodeName.Equals("info", StringComparison.OrdinalIgnoreCase);
            if (nodeIsInfoBranch)
            {
                foreach ((WzImageProperty Branch, string RelativePath) nestedInfoBranch in EnumerateClientSummonedUolHeuristicInfoBranches(
                             node,
                             relativePathPrefix: normalizedNodeName,
                             depthRemaining: ClientSummonedUolHeuristicInfoTraversalDepth))
                {
                    yield return nestedInfoBranch;
                }
            }

            foreach (WzImageProperty child in node.WzProperties)
            {
                if (child == null || string.IsNullOrWhiteSpace(child.Name))
                {
                    continue;
                }

                yield return (child, child.Name);
                if (!child.Name.Equals("info", StringComparison.OrdinalIgnoreCase))
                {
                    foreach ((WzImageProperty Branch, string RelativePath) nestedSkillBranch in EnumerateClientSummonedUolHeuristicSkillBranches(
                                 child,
                                 relativePathPrefix: child.Name,
                                 depthRemaining: ClientSummonedUolHeuristicSkillTraversalDepth))
                    {
                        yield return nestedSkillBranch;
                    }

                    continue;
                }

                foreach ((WzImageProperty Branch, string RelativePath) nestedInfoBranch in EnumerateClientSummonedUolHeuristicInfoBranches(
                             child,
                             relativePathPrefix: child.Name,
                             depthRemaining: ClientSummonedUolHeuristicInfoTraversalDepth))
                {
                    yield return nestedInfoBranch;
                }
            }
        }

        private static IEnumerable<(WzImageProperty Branch, string RelativePath)> EnumerateClientSummonedUolHeuristicSkillBranches(
            WzImageProperty node,
            string relativePathPrefix,
            int depthRemaining)
        {
            if (node?.WzProperties == null || depthRemaining <= 0)
            {
                yield break;
            }

            foreach (WzImageProperty child in node.WzProperties)
            {
                if (child == null || string.IsNullOrWhiteSpace(child.Name))
                {
                    continue;
                }

                string relativePath = string.IsNullOrWhiteSpace(relativePathPrefix)
                    ? child.Name
                    : $"{relativePathPrefix}/{child.Name}";
                yield return (child, relativePath);
                foreach ((WzImageProperty Branch, string RelativePath) nestedBranch in EnumerateClientSummonedUolHeuristicSkillBranches(
                             child,
                             relativePath,
                             depthRemaining - 1))
                {
                    yield return nestedBranch;
                }
            }
        }

        private static IEnumerable<(WzImageProperty Branch, string RelativePath)> EnumerateClientSummonedUolHeuristicInfoBranches(
            WzImageProperty node,
            string relativePathPrefix,
            int depthRemaining)
        {
            if (node?.WzProperties == null || depthRemaining < 0)
            {
                yield break;
            }

            foreach (WzImageProperty child in node.WzProperties)
            {
                if (child == null || string.IsNullOrWhiteSpace(child.Name))
                {
                    continue;
                }

                string relativePath = string.IsNullOrWhiteSpace(relativePathPrefix)
                    ? child.Name
                    : $"{relativePathPrefix}/{child.Name}";
                yield return (child, relativePath);
                foreach ((WzImageProperty Branch, string RelativePath) nestedBranch in EnumerateClientSummonedUolHeuristicInfoBranches(
                             child,
                             relativePath,
                             depthRemaining - 1))
                {
                    yield return nestedBranch;
                }
            }
        }

        private static string GetClientSummonedUolCandidateValue(WzImageProperty node, string propertyName)
        {
            WzImageProperty child = node?[propertyName];
            return GetClientSummonedUolCandidateValue(child);
        }

        private static string GetClientSummonedUolCandidateValue(WzImageProperty child)
        {
            switch (child)
            {
                case WzStringProperty stringProperty when !string.IsNullOrWhiteSpace(stringProperty.Value):
                    return stringProperty.Value;
                case WzUOLProperty uolProperty when !string.IsNullOrWhiteSpace(uolProperty.Value):
                    return uolProperty.Value;
                case WzUOLProperty uolProperty:
                    string linkedValue = uolProperty.GetString();
                    return string.IsNullOrWhiteSpace(linkedValue) ? null : linkedValue;
                case WzIntProperty intProperty when intProperty.Value > 0:
                    return intProperty.Value.ToString(CultureInfo.InvariantCulture);
                case WzShortProperty shortProperty when shortProperty.Value > 0:
                    return shortProperty.Value.ToString(CultureInfo.InvariantCulture);
                case WzLongProperty longProperty when longProperty.Value > 0:
                    return longProperty.Value.ToString(CultureInfo.InvariantCulture);
                case WzFloatProperty floatProperty when TryFormatClientSummonedUolFloatSkillValue(floatProperty.Value, out string floatValue):
                    return floatValue;
                case WzDoubleProperty doubleProperty when TryFormatClientSummonedUolNumericSkillValue(doubleProperty.Value, out string doubleValue):
                    return doubleValue;
                default:
                    return null;
            }
        }

        private static bool TryFormatClientSummonedUolNumericSkillValue(double value, out string formattedValue)
        {
            formattedValue = null;
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                return false;
            }

            double roundedValue = Math.Round(value);
            if (Math.Abs(value - roundedValue) > double.Epsilon
                || roundedValue > int.MaxValue)
            {
                return false;
            }

            formattedValue = ((int)roundedValue).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        private static bool TryFormatClientSummonedUolFloatSkillValue(float value, out string formattedValue)
        {
            formattedValue = null;
            const int maxConsecutiveIntegerInSinglePrecision = 16_777_216;
            return value <= maxConsecutiveIntegerInSinglePrecision
                   && TryFormatClientSummonedUolNumericSkillValue(value, out formattedValue);
        }

        private static string[] BuildClientSummonedUolCandidateContextPathParts(
            WzImageProperty node,
            string propertyName,
            WzImageProperty skillNode)
        {
            string normalizedNodePath = NormalizeClientSummonedUolFullPath(node?.FullPath)
                                        ?? BuildMountedSkillRootPathFromSkillNode(skillNode);
            if (string.IsNullOrWhiteSpace(normalizedNodePath))
            {
                return null;
            }

            var parts = normalizedNodePath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            if (!string.IsNullOrWhiteSpace(propertyName))
            {
                parts.AddRange(propertyName
                    .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
            }

            return parts.ToArray();
        }

        private static string BuildMountedSkillRootPathFromSkillNode(WzImageProperty skillNode)
        {
            return TryParseRequiredSkillId(skillNode?.Name, out int skillId)
                ? $"Skill/{skillId / 10000}.img/skill/{skillId}"
                : null;
        }

        private static string NormalizeClientSummonedUolCandidatePath(
            string value,
            string[] contextPathParts)
        {
            return EnumerateNormalizedClientSummonedUolCandidatePaths(value, contextPathParts).FirstOrDefault();
        }

        private static IEnumerable<string> EnumerateNormalizedClientSummonedUolCandidatePaths(
            string value,
            string[] contextPathParts)
        {
            var yieldedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string normalizedPath = NormalizeClientSummonedUolPath(value);
            if (!string.IsNullOrWhiteSpace(normalizedPath) && yieldedPaths.Add(normalizedPath))
            {
                yield return normalizedPath;
            }

            if (contextPathParts == null || contextPathParts.Length == 0)
            {
                yield break;
            }

            foreach (string token in EnumerateClientSummonedUolPathTokensFromValue(value))
            {
                normalizedPath = NormalizeClientSummonedUolPathToken(token, contextPathParts);
                if (!string.IsNullOrWhiteSpace(normalizedPath) && yieldedPaths.Add(normalizedPath))
                {
                    yield return normalizedPath;
                }
            }

            foreach (string fallbackRootPath in EnumerateClientSummonedUolFallbackRootPathsFromValue(value, contextPathParts))
            {
                if (yieldedPaths.Add(fallbackRootPath))
                {
                    yield return fallbackRootPath;
                }
            }
        }

        private static IEnumerable<string> EnumerateClientSummonedUolFallbackRootPathsFromValue(
            string value,
            string[] contextPathParts)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            int contextSkillId = ResolveClientSummonedUolFallbackContextSkillId(contextPathParts);
            var yieldedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (int linkedSkillId in EnumerateClientSummonedUolFallbackSkillIdsFromValue(value, contextSkillId))
            {
                if (!LooksLikeClientSummonedUolInferredSkillId(linkedSkillId))
                {
                    continue;
                }

                string rootPath = $"Skill/{linkedSkillId / 10000}.img/skill/{linkedSkillId}";
                if (yieldedPaths.Add(rootPath))
                {
                    yield return rootPath;
                }
            }
        }

        private static int ResolveClientSummonedUolFallbackContextSkillId(string[] contextPathParts)
        {
            if (contextPathParts == null || contextPathParts.Length == 0)
            {
                return 0;
            }

            string normalizedContextPath = NormalizeClientSummonedUolPathSegments(
                contextPathParts.Take(Math.Max(contextPathParts.Length - 1, 0)));
            if (string.IsNullOrWhiteSpace(normalizedContextPath))
            {
                normalizedContextPath = NormalizeClientSummonedUolPathSegments(contextPathParts);
            }

            return TryParseClientSummonedUolRootSkillId(normalizedContextPath, out int contextSkillId)
                ? contextSkillId
                : 0;
        }

        private static IEnumerable<int> EnumerateClientSummonedUolFallbackSkillIdsFromValue(
            string value,
            int contextSkillId)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            var yieldedSkillIds = new HashSet<int>();
            int tokenStart = -1;
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsDigit(value[i]))
                {
                    tokenStart = tokenStart < 0 ? i : tokenStart;
                    continue;
                }

                foreach (int skillId in EnumerateClientSummonedUolFallbackSkillIdsFromToken(
                             value,
                             tokenStart,
                             i - tokenStart,
                             yieldedSkillIds,
                             contextSkillId))
                {
                    yield return skillId;
                }

                tokenStart = -1;
            }

            foreach (int skillId in EnumerateClientSummonedUolFallbackSkillIdsFromToken(
                         value,
                         tokenStart,
                         value.Length - tokenStart,
                         yieldedSkillIds,
                         contextSkillId))
            {
                yield return skillId;
            }
        }

        private static IEnumerable<int> EnumerateClientSummonedUolFallbackSkillIdsFromToken(
            string value,
            int tokenStart,
            int tokenLength,
            HashSet<int> yieldedSkillIds,
            int contextSkillId)
        {
            int tokenEnd = tokenStart + tokenLength;
            if (tokenStart < 0
                || tokenLength <= 0
                || yieldedSkillIds == null
                || tokenLength > 10)
            {
                yield break;
            }

            if (tokenEnd + 4 <= value.Length
                && value.Substring(tokenEnd, 4).Equals(".img", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            if (!int.TryParse(
                    value.Substring(tokenStart, tokenLength),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int skillId)
                || skillId <= 0
                || !yieldedSkillIds.Add(skillId))
            {
                yield break;
            }

            yield return skillId;

            if (TryBuildClientSummonedUolContextualSkillId(contextSkillId, skillId, out int contextualSkillId)
                && yieldedSkillIds.Add(contextualSkillId))
            {
                yield return contextualSkillId;
            }
        }

        private static bool TryBuildClientSummonedUolContextualSkillId(
            int contextSkillId,
            int tokenSkillId,
            out int contextualSkillId)
        {
            contextualSkillId = 0;
            if (contextSkillId <= 0
                || !LooksLikeClientSummonedUolInferredSkillId(tokenSkillId)
                || tokenSkillId >= 1_000_000
                || tokenSkillId >= 100_000)
            {
                return false;
            }

            int contextJobId = contextSkillId / 10_000;
            if (contextJobId <= 0)
            {
                return false;
            }

            contextualSkillId = contextJobId * 10_000 + tokenSkillId;
            return contextualSkillId != tokenSkillId
                   && LooksLikeClientSummonedUolInferredSkillId(contextualSkillId);
        }

        private static string BuildClientSummonedUolPathFromSkillNode(WzImageProperty skillNode)
        {
            WzImageProperty summonProperty = skillNode?[ClientSummonedUolBranchName];
            if (summonProperty == null)
            {
                return null;
            }

            string normalizedPath = NormalizeClientSummonedUolFullPath(ResolveLinkedProperty(summonProperty)?.FullPath);
            return !string.IsNullOrWhiteSpace(normalizedPath)
                ? normalizedPath
                : NormalizeClientSummonedUolFullPath(summonProperty.FullPath);
        }

        internal static string NormalizeClientSummonedUolPathForTest(string summonedUolPath)
        {
            return NormalizeClientSummonedUolPath(summonedUolPath);
        }

        internal static string NormalizeClientSummonedUolFullPathForTest(string summonedUolFullPath)
        {
            return NormalizeClientSummonedUolFullPath(summonedUolFullPath);
        }

        internal static string NormalizeClientSummonedUolCandidatePathForTest(
            string candidateValue,
            int contextSkillId,
            bool infoNodeContext = true)
        {
            if (!TryParseRequiredSkillId(contextSkillId.ToString(CultureInfo.InvariantCulture), out int skillId))
            {
                return NormalizeClientSummonedUolPath(candidateValue);
            }

            string contextPath = $"Skill/{skillId / 10000}.img/skill/{skillId}";
            if (infoNodeContext)
            {
                contextPath += "/info";
            }

            string[] contextPathParts = contextPath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Concat(new[] { "sSummonedUOL" })
                .ToArray();
            return NormalizeClientSummonedUolCandidatePath(candidateValue, contextPathParts);
        }

        internal static IReadOnlyList<string> ResolveClientSummonedUolCandidatePathsForTest(
            string candidateValue,
            int contextSkillId,
            bool infoNodeContext = true)
        {
            if (!TryParseRequiredSkillId(contextSkillId.ToString(CultureInfo.InvariantCulture), out int skillId))
            {
                string normalizedPath = NormalizeClientSummonedUolPath(candidateValue);
                return string.IsNullOrWhiteSpace(normalizedPath)
                    ? Array.Empty<string>()
                    : new[] { normalizedPath };
            }

            string contextPath = $"Skill/{skillId / 10000}.img/skill/{skillId}";
            if (infoNodeContext)
            {
                contextPath += "/info";
            }

            string[] contextPathParts = contextPath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Concat(new[] { "sSummonedUOL" })
                .ToArray();
            return EnumerateNormalizedClientSummonedUolCandidatePaths(candidateValue, contextPathParts).ToArray();
        }

        internal static IReadOnlyList<string> ResolveClientSummonedUolPathsForTest(
            WzImageProperty skillNode,
            WzImageProperty infoNode)
        {
            return ResolveClientSummonedUolPaths(skillNode, infoNode);
        }

        internal static string NormalizeClientSkillAssetUolCandidatePathForTest(
            string candidateValue,
            int contextSkillId,
            string propertyName,
            bool infoNodeContext = true)
        {
            if (!TryParseRequiredSkillId(contextSkillId.ToString(CultureInfo.InvariantCulture), out int skillId))
            {
                return NormalizeClientSummonedUolPath(candidateValue);
            }

            string contextPath = $"Skill/{skillId / 10000}.img/skill/{skillId}";
            if (infoNodeContext)
            {
                contextPath += "/info";
            }

            string[] contextPathParts = contextPath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Concat(new[] { string.IsNullOrWhiteSpace(propertyName) ? "uol" : propertyName })
                .ToArray();
            return NormalizeClientSummonedUolCandidatePath(candidateValue, contextPathParts);
        }

        internal static string ResolveClientSkillAssetRootUolPathForTest(
            WzImageProperty skillNode,
            WzImageProperty infoNode,
            string propertyName)
        {
            IReadOnlyList<string> propertyNames = ResolveClientSkillAssetUolPropertyNamesForTest(propertyName);
            return ResolveClientSkillAssetUolPathResolution(skillNode, infoNode, propertyNames).RootPath;
        }

        private static IReadOnlyList<string> ResolveClientSkillAssetUolPropertyNamesForTest(string propertyName)
        {
            if (ClientTileUolPropertyNames.Contains(propertyName, StringComparer.OrdinalIgnoreCase))
            {
                return ClientTileUolPropertyNames;
            }

            if (ClientBallUolPropertyNames.Contains(propertyName, StringComparer.OrdinalIgnoreCase))
            {
                return ClientBallUolPropertyNames;
            }

            if (ClientFlipBallUolPropertyNames.Contains(propertyName, StringComparer.OrdinalIgnoreCase))
            {
                return ClientFlipBallUolPropertyNames;
            }

            if (ClientSummonedUolPropertyNames.Contains(propertyName, StringComparer.OrdinalIgnoreCase))
            {
                return ClientSummonedUolPropertyNames;
            }

            return string.IsNullOrWhiteSpace(propertyName)
                ? Array.Empty<string>()
                : new[] { propertyName };
        }

        internal static (
            string RootPath,
            IReadOnlyDictionary<int, string> CharacterLevelPaths,
            IReadOnlyDictionary<int, string> LevelPaths)
            ResolveClientSkillAssetUolPathResolutionForTest(
                IReadOnlyList<(string Value, string ContextPath)> candidates)
        {
            var resolution = new ClientSkillAssetUolPathResolution(
                RootPath: null,
                CharacterLevelPaths: new SortedDictionary<int, string>(),
                LevelPaths: new Dictionary<int, string>());
            if (candidates == null)
            {
                return (resolution.RootPath, resolution.CharacterLevelPaths, resolution.LevelPaths);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                (string value, string contextPath) = candidates[i];
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                string[] contextPathParts = string.IsNullOrWhiteSpace(contextPath)
                    ? Array.Empty<string>()
                    : contextPath
                        .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string normalizedPath = NormalizeClientSummonedUolCandidatePath(value, contextPathParts);
                if (string.IsNullOrWhiteSpace(normalizedPath))
                {
                    continue;
                }

                if (TryResolveClientSkillAssetUolVariantLevel(contextPathParts, out bool isCharacterLevelVariant, out int variantLevel))
                {
                    IDictionary<int, string> target = isCharacterLevelVariant
                        ? resolution.CharacterLevelPaths
                        : resolution.LevelPaths;
                    if (!target.ContainsKey(variantLevel))
                    {
                        target[variantLevel] = normalizedPath;
                    }
                }
                else if (string.IsNullOrWhiteSpace(resolution.RootPath))
                {
                    resolution = resolution with { RootPath = normalizedPath };
                }
            }

            return (resolution.RootPath, resolution.CharacterLevelPaths, resolution.LevelPaths);
        }

        internal static IEnumerable<int> EnumerateVisibleSummonSourceCandidateSkillIdsFromClientSummonedUolPathForTest(
            string summonedUolPath,
            WzImageProperty resolvedProperty = null,
            Func<int, SkillData> linkedSkillResolver = null,
            Func<int, IReadOnlyList<int>> reverseAffectedSkillResolver = null)
        {
            return EnumerateVisibleSummonSourceCandidateSkillIdsFromClientSummonedUolPath(
                summonedUolPath,
                resolvedProperty,
                linkedSkillResolver,
                reverseAffectedSkillResolver);
        }

        internal bool TryResolveClientSummonedUolPropertyForTest(string summonedUolPath, out WzImageProperty summonNode)
        {
            return TryResolveClientSummonedUolProperty(summonedUolPath, out summonNode);
        }

        internal static bool TryParseClientSummonedUolRootSkillIdForTest(string summonedUolPath, out int skillId)
        {
            return TryParseClientSummonedUolRootSkillId(NormalizeClientSummonedUolPath(summonedUolPath), out skillId);
        }

        internal static IEnumerable<int> EnumerateClientSummonedUolLinkedSkillIdsForTest(string summonedUolPath)
        {
            return EnumerateClientSummonedUolLinkedSkillIds(NormalizeClientSummonedUolPath(summonedUolPath));
        }

        internal static IEnumerable<int> EnumerateClientSummonedUolLinkedSkillIdsForTest(
            string summonedUolPath,
            WzImageProperty resolvedProperty)
        {
            return EnumerateClientSummonedUolLinkedSkillIds(
                NormalizeClientSummonedUolPath(summonedUolPath),
                resolvedProperty);
        }

        internal static bool TryParseNormalizedSkillAnimationPathForTest(
            string normalizedSkillAssetPath,
            out string imageName,
            out string[] propertySegments)
        {
            return TryParseNormalizedSkillAnimationPath(normalizedSkillAssetPath, out imageName, out propertySegments);
        }

        private static string NormalizeClientSummonedUolPath(string summonedUolPath)
        {
            if (string.IsNullOrWhiteSpace(summonedUolPath))
            {
                return null;
            }

            string normalizedPath = NormalizeClientSummonedUolEncodedPathSyntax(summonedUolPath)
                .Replace('\\', '/')
                .Trim()
                .Trim('"', '\'')
                .TrimStart('/');

            if (IsPlainNumericClientSummonedUolToken(normalizedPath))
            {
                return null;
            }

            const string wzRootPrefix = "wz/";
            if (normalizedPath.StartsWith(wzRootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = normalizedPath[wzRootPrefix.Length..];
            }

            const string skillArchivePrefix = "Skill.wz/";
            if (normalizedPath.StartsWith(skillArchivePrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = normalizedPath[skillArchivePrefix.Length..].TrimStart('/');
                if (!normalizedPath.StartsWith("Skill/", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedPath = $"Skill/{normalizedPath}";
                }
            }

            normalizedPath = normalizedPath.TrimStart('/');
            if (!normalizedPath.StartsWith("Skill/", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = $"Skill/{normalizedPath.TrimStart('/')}";
            }

            string[] parts = normalizedPath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0
                || !parts[0].Equals("Skill", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (TryNormalizeMountedSkillRootClientSummonedUolParts(parts, out string normalizedMountedRootPath))
            {
                return normalizedMountedRootPath;
            }

            string normalizedSegmentPath = NormalizeClientSummonedUolPathSegments(parts);
            if (!string.IsNullOrWhiteSpace(normalizedSegmentPath)
                && TryParseClientSummonedUolRootSkillId(normalizedSegmentPath, out _))
            {
                return normalizedSegmentPath;
            }

            string embeddedPathToken = ExtractEmbeddedClientSummonedUolPathToken(normalizedPath);
            if (string.IsNullOrWhiteSpace(embeddedPathToken)
                || string.Equals(embeddedPathToken, normalizedPath, StringComparison.Ordinal))
            {
                return null;
            }

            return NormalizeClientSummonedUolPath(embeddedPathToken);
        }

        private static bool IsPlainNumericClientSummonedUolToken(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                   && value.IndexOf('/') < 0
                   && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }

        private static string NormalizeClientSummonedUolFullPath(string summonedUolFullPath)
        {
            if (string.IsNullOrWhiteSpace(summonedUolFullPath))
            {
                return null;
            }

            string normalizedPath = NormalizeClientSummonedUolEncodedPathSyntax(summonedUolFullPath)
                .Replace('\\', '/')
                .Trim()
                .Trim('"', '\'')
                .TrimStart('/');

            string[] parts = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return null;
            }

            if (parts[0].Equals("Skill", StringComparison.OrdinalIgnoreCase)
                || parts[0].Equals("Skill.wz", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeClientSummonedUolPath(normalizedPath);
            }

            if (parts[0].EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeClientSummonedUolPath($"Skill/{normalizedPath}");
            }

            return null;
        }

        private static string NormalizeClientSummonedUolEncodedPathSyntax(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            string normalized = token;
            for (int pass = 0; pass < 3; pass++)
            {
                string decoded = NormalizeClientSummonedUolEncodedPathSyntaxOnce(
                    NormalizeClientSummonedUolEntityEncodedPathSyntax(normalized));
                if (string.Equals(decoded, normalized, StringComparison.Ordinal))
                {
                    return decoded;
                }

                normalized = decoded;
            }

            return normalized;
        }

        private static string NormalizeClientSummonedUolEntityEncodedPathSyntax(string token)
        {
            if (string.IsNullOrWhiteSpace(token)
                || token.IndexOf('&') < 0)
            {
                return token ?? string.Empty;
            }

            var builder = new System.Text.StringBuilder(token.Length);
            for (int i = 0; i < token.Length; i++)
            {
                char current = token[i];
                if (current == '&'
                    && TryParseClientSummonedUolEntityEncodedChar(
                        token,
                        i,
                        out char decoded,
                        out int consumedLength)
                    && IsClientSummonedUolDecodedPathCharacter(decoded))
                {
                    builder.Append(decoded == '\\' ? '/' : decoded);
                    i += consumedLength - 1;
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static bool TryParseClientSummonedUolEntityEncodedChar(
            string token,
            int startIndex,
            out char decoded,
            out int consumedLength)
        {
            decoded = '\0';
            consumedLength = 0;
            if (string.IsNullOrEmpty(token)
                || startIndex < 0
                || startIndex >= token.Length
                || token[startIndex] != '&')
            {
                return false;
            }

            int semicolonIndex = token.IndexOf(';', startIndex + 1);
            if (semicolonIndex < 0 || semicolonIndex - startIndex > 10)
            {
                return false;
            }

            string entity = token.Substring(startIndex + 1, semicolonIndex - startIndex - 1);
            if (string.IsNullOrWhiteSpace(entity))
            {
                return false;
            }

            consumedLength = semicolonIndex - startIndex + 1;
            switch (entity.ToLowerInvariant())
            {
                case "quot":
                    decoded = '"';
                    return true;
                case "apos":
                    decoded = '\'';
                    return true;
                case "amp":
                    decoded = '&';
                    return true;
                case "lt":
                    decoded = '<';
                    return true;
                case "gt":
                    decoded = '>';
                    return true;
            }

            if (entity[0] != '#')
            {
                return false;
            }

            NumberStyles style = NumberStyles.Integer;
            string numericToken = entity.Substring(1);
            if (numericToken.Length > 1
                && (numericToken[0] == 'x' || numericToken[0] == 'X'))
            {
                style = NumberStyles.HexNumber;
                numericToken = numericToken.Substring(1);
            }

            if (!int.TryParse(numericToken, style, CultureInfo.InvariantCulture, out int codePoint)
                || codePoint < 0
                || codePoint > char.MaxValue)
            {
                return false;
            }

            decoded = (char)codePoint;
            return true;
        }

        private static string NormalizeClientSummonedUolEncodedPathSyntaxOnce(string token)
        {
            var builder = new System.Text.StringBuilder(token.Length);
            for (int i = 0; i < token.Length; i++)
            {
                char current = token[i];
                if (current == '\\' && i < token.Length - 1)
                {
                    if (i < token.Length - 5
                        && (token[i + 1] == 'u' || token[i + 1] == 'U')
                        && TryParseClientSummonedUolUnicodeEscapedChar(
                            token,
                            i + 2,
                            out char unicodeDecoded)
                        && IsClientSummonedUolDecodedPathCharacter(unicodeDecoded))
                    {
                        builder.Append(unicodeDecoded == '\\' ? '/' : unicodeDecoded);
                        i += 5;
                        continue;
                    }

                    char escaped = token[i + 1];
                    if (IsClientSummonedUolEscapedPathSyntaxCharacter(escaped))
                    {
                        builder.Append(escaped == '\\' ? '/' : escaped);
                        i++;
                        continue;
                    }
                }

                if (current == '%'
                    && i < token.Length - 2
                    && TryParseClientSummonedUolPercentEncodedChar(token[i + 1], token[i + 2], out char decoded)
                    && IsClientSummonedUolDecodedPathCharacter(decoded))
                {
                    builder.Append(decoded == '\\' ? '/' : decoded);
                    i += 2;
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static bool IsClientSummonedUolEscapedPathSyntaxCharacter(char character)
        {
            return character == '/'
                   || character == '\\'
                   || character == ':'
                   || character == '"'
                   || character == '\''
                   || character == '('
                   || character == ')'
                   || character == '['
                   || character == ']'
                   || character == '{'
                   || character == '}'
                   || character == '<'
                   || character == '>';
        }

        private static bool IsClientSummonedUolDecodedPathCharacter(char character)
        {
            return (character >= 0x20 && character <= 0x7E)
                   || char.IsWhiteSpace(character);
        }

        private static bool TryParseClientSummonedUolUnicodeEscapedChar(
            string token,
            int firstHexIndex,
            out char decoded)
        {
            decoded = '\0';
            if (string.IsNullOrEmpty(token)
                || firstHexIndex < 0
                || firstHexIndex + 3 >= token.Length)
            {
                return false;
            }

            int value = 0;
            for (int i = 0; i < 4; i++)
            {
                if (!TryParseClientSummonedUolHexDigit(token[firstHexIndex + i], out int digit))
                {
                    return false;
                }

                value = (value << 4) | digit;
            }

            decoded = (char)value;
            return true;
        }

        private static bool TryParseClientSummonedUolPercentEncodedChar(
            char firstHexChar,
            char secondHexChar,
            out char decoded)
        {
            decoded = '\0';
            if (!TryParseClientSummonedUolHexDigit(firstHexChar, out int high)
                || !TryParseClientSummonedUolHexDigit(secondHexChar, out int low))
            {
                return false;
            }

            decoded = (char)((high << 4) | low);
            return true;
        }

        private static bool TryParseClientSummonedUolHexDigit(char character, out int value)
        {
            value = 0;
            if (character >= '0' && character <= '9')
            {
                value = character - '0';
                return true;
            }

            char normalized = char.ToUpperInvariant(character);
            if (normalized >= 'A' && normalized <= 'F')
            {
                value = normalized - 'A' + 10;
                return true;
            }

            return false;
        }

        private static IEnumerable<int> EnumerateVisibleSummonSourceCandidateSkillIdsFromClientSummonedUolPath(
            string summonedUolPath,
            WzImageProperty resolvedProperty = null,
            Func<int, SkillData> linkedSkillResolver = null,
            Func<int, IReadOnlyList<int>> reverseAffectedSkillResolver = null)
        {
            string normalizedPath = NormalizeClientSummonedUolPath(summonedUolPath);
            var yieldedSkillIds = new HashSet<int>();
            foreach (int resolvedSkillId in EnumerateVisibleSummonSourceCandidateSeedSkillIds(normalizedPath, resolvedProperty))
            {
                SkillData resolvedSkill = linkedSkillResolver?.Invoke(resolvedSkillId);
                if (resolvedSkill == null)
                {
                    continue;
                }

                foreach (int linkedSkillId in EnumerateSummonSourceCandidateSkillIds(
                             resolvedSkill,
                             linkedSkillResolver,
                             reverseAffectedSkillResolver))
                {
                    if (linkedSkillId > 0
                        && linkedSkillId != resolvedSkillId
                        && yieldedSkillIds.Add(linkedSkillId))
                    {
                        yield return linkedSkillId;
                    }
                }
            }
        }

        private static IEnumerable<int> EnumerateVisibleSummonSourceCandidateSeedSkillIds(
            string normalizedPath,
            WzImageProperty resolvedProperty)
        {
            var yieldedSkillIds = new HashSet<int>();
            if (TryParseClientSummonedUolRootSkillId(normalizedPath, out int rootSkillId)
                && yieldedSkillIds.Add(rootSkillId))
            {
                yield return rootSkillId;
            }

            foreach (int linkedSkillId in EnumerateClientSummonedUolLinkedSkillIds(normalizedPath, resolvedProperty))
            {
                if (linkedSkillId > 0 && yieldedSkillIds.Add(linkedSkillId))
                {
                    yield return linkedSkillId;
                }
            }
        }

        private static bool TryParseClientSummonedUolRootSkillId(string normalizedPath, out int skillId)
        {
            skillId = 0;
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return false;
            }

            string[] parts = normalizedPath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3
                || !parts[0].Equals("Skill", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            for (int i = 2; i < parts.Length; i++)
            {
                if (TryParseRequiredSkillId(parts[i], out skillId))
                {
                    return true;
                }
            }

            skillId = 0;
            return false;
        }

        private static IEnumerable<int> EnumerateClientSummonedUolLinkedSkillIds(
            string normalizedPath,
            WzImageProperty resolvedProperty = null)
        {
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                yield break;
            }

            string[] parts = normalizedPath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4
                || !parts[0].Equals("Skill", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            var yieldedSkillIds = new HashSet<int>();
            if (TryParseClientSummonedUolRootSkillId(normalizedPath, out int rootSkillId))
            {
                yieldedSkillIds.Add(rootSkillId);
            }

            for (int i = 3; i < parts.Length; i++)
            {
                if (!IsClientSummonedUolLinkedSkillIdSegment(parts[i - 1])
                    || !TryParseRequiredSkillId(parts[i], out int linkedSkillId)
                    || !yieldedSkillIds.Add(linkedSkillId))
                {
                    continue;
                }

                yield return linkedSkillId;
            }

            foreach (int inferredSkillId in EnumerateClientSummonedUolInferredSkillIdSegments(parts, rootSkillId))
            {
                if (yieldedSkillIds.Add(inferredSkillId))
                {
                    yield return inferredSkillId;
                }
            }

            if (resolvedProperty == null
                || (!IsClientSummonedUolLinkedSkillValueLeaf(parts)
                    && !IsClientSummonedUolLinkedSkillBranchLeaf(parts)))
            {
                yield break;
            }

            foreach (int linkedSkillId in EnumerateClientSummonedUolLinkedSkillIdsFromResolvedProperty(parts, resolvedProperty))
            {
                if (linkedSkillId > 0 && yieldedSkillIds.Add(linkedSkillId))
                {
                    yield return linkedSkillId;
                }
            }
        }

        private static bool IsClientSummonedUolLinkedSkillIdSegment(string previousSegment)
        {
            return previousSegment != null
                   && (previousSegment.Equals("req", StringComparison.OrdinalIgnoreCase)
                       || previousSegment.Equals("psdSkill", StringComparison.OrdinalIgnoreCase)
                       || previousSegment.Equals("requireSkill", StringComparison.OrdinalIgnoreCase)
                       || previousSegment.Equals("affectedSkill", StringComparison.OrdinalIgnoreCase)
                       || previousSegment.Equals("dummyOf", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsClientSummonedUolLinkedSkillValueLeaf(string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return false;
            }

            string leafSegment = parts[^1];
            if (leafSegment.Equals("requireSkill", StringComparison.OrdinalIgnoreCase)
                || leafSegment.Equals("affectedSkill", StringComparison.OrdinalIgnoreCase)
                || leafSegment.Equals("dummyOf", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return parts.Length >= 2
                   && IsClientSummonedUolLinkedSkillIdSegment(parts[^2])
                   && TryParseRequiredSkillId(leafSegment, out _);
        }

        private static bool IsClientSummonedUolLinkedSkillBranchLeaf(string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return false;
            }

            string leafSegment = parts[^1];
            return leafSegment.Equals("req", StringComparison.OrdinalIgnoreCase)
                   || leafSegment.Equals("psdSkill", StringComparison.OrdinalIgnoreCase)
                   || leafSegment.Equals("info", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsClientSummonedUolReqOrPassiveValueLeaf(string[] parts)
        {
            if (parts == null || parts.Length < 2)
            {
                return false;
            }

            string parentSegment = parts[^2];
            return (parentSegment.Equals("req", StringComparison.OrdinalIgnoreCase)
                    || parentSegment.Equals("psdSkill", StringComparison.OrdinalIgnoreCase))
                   && TryParseRequiredSkillId(parts[^1], out _);
        }

        private static IEnumerable<int> EnumerateClientSummonedUolInferredSkillIdSegments(string[] parts, int rootSkillId)
        {
            if (parts == null || parts.Length == 0)
            {
                yield break;
            }

            for (int i = 2; i < parts.Length; i++)
            {
                if (!TryParseRequiredSkillId(parts[i], out int candidateSkillId)
                    || candidateSkillId == rootSkillId
                    || !LooksLikeClientSummonedUolInferredSkillId(candidateSkillId))
                {
                    continue;
                }

                yield return candidateSkillId;
            }
        }

        private static bool LooksLikeClientSummonedUolInferredSkillId(int skillId)
        {
            // Filter out common frame or level-style numeric path segments while keeping beginner IDs.
            return skillId >= 1000;
        }

        private static IEnumerable<int> EnumerateClientSummonedUolLinkedSkillIdsFromResolvedProperty(
            string[] parts,
            WzImageProperty resolvedProperty)
        {
            if (parts == null || parts.Length == 0 || resolvedProperty == null)
            {
                yield break;
            }

            string leafSegment = parts[^1];
            if (leafSegment.Equals("req", StringComparison.OrdinalIgnoreCase)
                || leafSegment.Equals("psdSkill", StringComparison.OrdinalIgnoreCase))
            {
                foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromChildNames(resolvedProperty))
                {
                    yield return linkedSkillId;
                }

                foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromChildNameTokens(resolvedProperty))
                {
                    yield return linkedSkillId;
                }

                foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromReqOrPassiveBranchChildValues(
                             resolvedProperty,
                             parts))
                {
                    yield return linkedSkillId;
                }

                yield break;
            }

            if (leafSegment.Equals("info", StringComparison.OrdinalIgnoreCase))
            {
                foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromInfoLinkLeaves(resolvedProperty))
                {
                    yield return linkedSkillId;
                }

                foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromNestedClientSummonedUolInfoLeaves(
                             resolvedProperty,
                             parts))
                {
                    yield return linkedSkillId;
                }

                yield break;
            }

            if (IsClientSummonedUolReqOrPassiveValueLeaf(parts))
            {
                // req/* and psdSkill/* values are authored levels or toggles; linked skill ids come from the child names.
                yield break;
            }

            foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromResolvedClientSummonedUolOwnerBranches(
                         resolvedProperty,
                         parts))
            {
                yield return linkedSkillId;
            }

            foreach (int linkedSkillId in ParseLinkedSkillIds(resolvedProperty))
            {
                yield return linkedSkillId;
            }

            foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromClientSummonedUolValueProperty(resolvedProperty, parts))
            {
                yield return linkedSkillId;
            }
        }

        private static IEnumerable<int> EnumerateLinkedSkillIdsFromClientSummonedUolValueProperty(
            WzImageProperty resolvedProperty,
            string[] resolvedPathParts = null)
        {
            if (resolvedProperty == null)
            {
                yield break;
            }

            var yieldedSkillIds = new HashSet<int>();
            var processedValueTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var processedPathTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int contextSkillId = ResolveClientSummonedUolFallbackContextSkillId(resolvedPathParts);
            foreach (string valueText in EnumerateClientSummonedUolValueTexts(resolvedProperty))
            {
                if (string.IsNullOrWhiteSpace(valueText)
                    || !processedValueTexts.Add(valueText))
                {
                    continue;
                }

                foreach (int parsedLinkedSkillId in ParseLinkedSkillIds(valueText))
                {
                    if (parsedLinkedSkillId > 0 && yieldedSkillIds.Add(parsedLinkedSkillId))
                    {
                        yield return parsedLinkedSkillId;
                    }
                }

                foreach (int fallbackLinkedSkillId in EnumerateClientSummonedUolFallbackSkillIdsFromValue(valueText, contextSkillId))
                {
                    if (fallbackLinkedSkillId > 0
                        && LooksLikeClientSummonedUolInferredSkillId(fallbackLinkedSkillId)
                        && yieldedSkillIds.Add(fallbackLinkedSkillId))
                    {
                        yield return fallbackLinkedSkillId;
                    }
                }

                foreach (string pathToken in EnumerateClientSummonedUolPathTokensFromValue(valueText))
                {
                    if (string.IsNullOrWhiteSpace(pathToken)
                        || !processedPathTokens.Add(pathToken))
                    {
                        continue;
                    }

                    string normalizedPath = NormalizeClientSummonedUolPathToken(pathToken, resolvedPathParts);
                    if (string.IsNullOrWhiteSpace(normalizedPath))
                    {
                        continue;
                    }

                    if (TryParseClientSummonedUolRootSkillId(normalizedPath, out int rootSkillId)
                        && yieldedSkillIds.Add(rootSkillId))
                    {
                        yield return rootSkillId;
                    }

                    foreach (int linkedSkillId in EnumerateClientSummonedUolLinkedSkillIds(normalizedPath))
                    {
                        if (linkedSkillId > 0 && yieldedSkillIds.Add(linkedSkillId))
                        {
                            yield return linkedSkillId;
                        }
                    }
                }
            }
        }

        private static IEnumerable<int> EnumerateLinkedSkillIdsFromResolvedClientSummonedUolOwnerBranches(
            WzImageProperty resolvedProperty,
            string[] resolvedPathParts)
        {
            if (resolvedProperty?.WzProperties == null)
            {
                yield break;
            }

            WzImageProperty infoProperty = resolvedProperty["info"];
            if (infoProperty != null)
            {
                foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromInfoLinkLeaves(infoProperty))
                {
                    yield return linkedSkillId;
                }

                foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromNestedClientSummonedUolInfoLeaves(
                             infoProperty,
                             BuildResolvedClientSummonedUolChildPathParts(resolvedPathParts, "info")))
                {
                    yield return linkedSkillId;
                }
            }

            foreach (string branchName in new[] { "req", "psdSkill" })
            {
                WzImageProperty branchProperty = resolvedProperty[branchName];
                if (branchProperty == null)
                {
                    continue;
                }

                foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromChildNames(branchProperty))
                {
                    yield return linkedSkillId;
                }

                foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromChildNameTokens(branchProperty))
                {
                    yield return linkedSkillId;
                }

                foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromReqOrPassiveBranchChildValues(
                             branchProperty,
                             BuildResolvedClientSummonedUolChildPathParts(resolvedPathParts, branchName)))
                {
                    yield return linkedSkillId;
                }
            }
        }

        private static string NormalizeClientSummonedUolPathToken(string token, string[] resolvedPathParts)
        {
            string normalizedPath = NormalizeClientSummonedUolPath(token);
            if (!string.IsNullOrWhiteSpace(normalizedPath))
            {
                return normalizedPath;
            }

            normalizedPath = NormalizeClientSkillAssetBranchToken(token, resolvedPathParts);
            if (!string.IsNullOrWhiteSpace(normalizedPath))
            {
                return normalizedPath;
            }

            if (!LooksLikeClientSummonedUolRelativePathToken(token)
                || resolvedPathParts == null
                || resolvedPathParts.Length == 0)
            {
                return null;
            }

            string normalizedToken = token
                .Trim()
                .Trim(ClientSummonedUolTokenTrimChars)
                .Replace('\\', '/')
                .TrimStart('/');
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                return null;
            }

            int contextLength = resolvedPathParts.Length - 1;
            if (contextLength <= 0)
            {
                return null;
            }

            var combinedParts = new List<string>(contextLength + 8);
            for (int i = 0; i < contextLength; i++)
            {
                combinedParts.Add(resolvedPathParts[i]);
            }

            foreach (string tokenPart in normalizedToken.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                combinedParts.Add(tokenPart);
            }

            string normalizedCombinedPath = NormalizeClientSummonedUolPathSegments(combinedParts);
            if (string.IsNullOrWhiteSpace(normalizedCombinedPath))
            {
                return null;
            }

            string[] normalizedCombinedParts = normalizedCombinedPath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (TryNormalizeMountedSkillRootClientSummonedUolParts(
                    normalizedCombinedParts,
                    out string normalizedMountedRootPath))
            {
                return normalizedMountedRootPath;
            }

            return normalizedCombinedPath;
        }

        private static string NormalizeClientSkillAssetBranchToken(string token, string[] resolvedPathParts)
        {
            if (string.IsNullOrWhiteSpace(token)
                || resolvedPathParts == null
                || resolvedPathParts.Length == 0)
            {
                return null;
            }

            string normalizedToken = NormalizeClientSummonedUolEncodedPathSyntax(token)
                .Trim()
                .Trim(ClientSummonedUolTokenTrimChars)
                .Replace('\\', '/')
                .TrimStart('/');
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                return null;
            }

            string[] tokenParts = normalizedToken
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            tokenParts = TrimClientSkillAssetRelativeBranchPrefixes(tokenParts);
            if (tokenParts.Length == 0 || !IsClientSkillAssetBranchTokenAllowedForContext(tokenParts[0], resolvedPathParts))
            {
                return null;
            }

            int skillSegmentIndex = FindClientSummonedUolSkillIdSegmentIndex(resolvedPathParts);
            if (skillSegmentIndex < 0)
            {
                return null;
            }

            var combinedParts = new List<string>(skillSegmentIndex + tokenParts.Length + 1);
            for (int i = 0; i <= skillSegmentIndex; i++)
            {
                combinedParts.Add(resolvedPathParts[i]);
            }

            combinedParts.AddRange(tokenParts);
            string normalizedCombinedPath = NormalizeClientSummonedUolPathSegments(combinedParts);
            if (string.IsNullOrWhiteSpace(normalizedCombinedPath))
            {
                return null;
            }

            string[] normalizedCombinedParts = normalizedCombinedPath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (TryNormalizeMountedSkillRootClientSummonedUolParts(
                    normalizedCombinedParts,
                    out string normalizedMountedRootPath))
            {
                return normalizedMountedRootPath;
            }

            return normalizedCombinedPath;
        }

        private static string[] TrimClientSkillAssetRelativeBranchPrefixes(string[] tokenParts)
        {
            if (tokenParts == null || tokenParts.Length == 0)
            {
                return Array.Empty<string>();
            }

            int startIndex = 0;
            while (startIndex < tokenParts.Length
                   && (string.Equals(tokenParts[startIndex], ".", StringComparison.Ordinal)
                       || string.Equals(tokenParts[startIndex], "..", StringComparison.Ordinal)))
            {
                startIndex++;
            }

            if (startIndex <= 0)
            {
                return tokenParts;
            }

            return startIndex >= tokenParts.Length
                ? Array.Empty<string>()
                : tokenParts.Skip(startIndex).ToArray();
        }

        private static int FindClientSummonedUolSkillIdSegmentIndex(IReadOnlyList<string> pathParts)
        {
            if (pathParts == null)
            {
                return -1;
            }

            for (int i = 0; i < pathParts.Count - 1; i++)
            {
                if (string.Equals(pathParts[i], "skill", StringComparison.OrdinalIgnoreCase)
                    && TryParseRequiredSkillId(pathParts[i + 1], out _))
                {
                    return i + 1;
                }
            }

            return -1;
        }

        private static bool IsClientSkillAssetBranchTokenAllowedForContext(
            string branchToken,
            IReadOnlyList<string> contextPathParts)
        {
            if (string.IsNullOrWhiteSpace(branchToken) || contextPathParts == null || contextPathParts.Count == 0)
            {
                return false;
            }

            if (HasClientSkillAssetContext(contextPathParts, ClientTileUolPropertyNames, "tilePath", "tileUolPath"))
            {
                return string.Equals(branchToken, "tile", StringComparison.OrdinalIgnoreCase);
            }

            if (HasClientSkillAssetContext(contextPathParts, ClientBallUolPropertyNames, "ballPath", "ballUolPath"))
            {
                return string.Equals(branchToken, "ball", StringComparison.OrdinalIgnoreCase);
            }

            if (HasClientSkillAssetContext(contextPathParts, ClientFlipBallUolPropertyNames, "flipBallPath", "flipBallUolPath"))
            {
                return string.Equals(branchToken, "flipBall", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(branchToken, "ball", StringComparison.OrdinalIgnoreCase);
            }

            if (HasClientSkillAssetContext(contextPathParts, ClientSummonedUolPropertyNames, "summonPath", "summonedPath", "summonedUolPath"))
            {
                return string.Equals(branchToken, "summon", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(branchToken, "summoned", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool HasClientSkillAssetContext(
            IReadOnlyList<string> contextPathParts,
            IReadOnlyList<string> propertyNames,
            params string[] valueLeafNames)
        {
            if (contextPathParts == null || contextPathParts.Count == 0)
            {
                return false;
            }

            for (int i = contextPathParts.Count - 1; i >= 0; i--)
            {
                string segment = contextPathParts[i]?.Trim();
                if (string.IsNullOrWhiteSpace(segment))
                {
                    continue;
                }

                if (propertyNames?.Contains(segment, StringComparer.OrdinalIgnoreCase) == true)
                {
                    return true;
                }

                if (valueLeafNames?.Contains(segment, StringComparer.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeClientSummonedUolRelativePathToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalizedToken = token
                .Trim()
                .Trim(ClientSummonedUolTokenTrimChars)
                .Replace('\\', '/');

            return normalizedToken.StartsWith(".", StringComparison.Ordinal)
                   || normalizedToken.Contains("/")
                   || normalizedToken.IndexOf(".img", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IEnumerable<string> EnumerateClientSummonedUolValueTexts(WzImageProperty resolvedProperty)
        {
            switch (resolvedProperty)
            {
                case WzStringProperty stringProperty when !string.IsNullOrWhiteSpace(stringProperty.Value):
                    yield return stringProperty.Value;
                    yield break;
                case WzUOLProperty uolProperty:
                    if (!string.IsNullOrWhiteSpace(uolProperty.Value))
                    {
                        yield return uolProperty.Value;
                    }

                    string linkedString = uolProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(linkedString))
                    {
                        yield return linkedString;
                    }

                    yield break;
                default:
                    string value = resolvedProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        yield return value;
                    }

                    yield break;
            }
        }

        private static IEnumerable<string> EnumerateClientSummonedUolPathTokensFromValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            var yieldedTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string token in EnumerateClientSummonedUolPathTokensFromDecodedValue(value))
            {
                if (!string.IsNullOrWhiteSpace(token) && yieldedTokens.Add(token))
                {
                    yield return token;
                }
            }

            string decodedValue = NormalizeClientSummonedUolEncodedPathSyntax(value);
            if (!string.Equals(decodedValue, value, StringComparison.Ordinal))
            {
                foreach (string token in EnumerateClientSummonedUolPathTokensFromDecodedValue(decodedValue))
                {
                    if (!string.IsNullOrWhiteSpace(token) && yieldedTokens.Add(token))
                    {
                        yield return token;
                    }
                }
            }
        }

        private static IEnumerable<string> EnumerateClientSummonedUolPathTokensFromDecodedValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            yield return value;
            foreach (string token in value.Split(new[] { '&', '|', ',', ';', '=', ':', '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmedToken = ExtractEmbeddedClientSummonedUolPathToken(token);
                if (!string.IsNullOrWhiteSpace(trimmedToken))
                {
                    yield return trimmedToken;
                }
            }
        }

        private static string ExtractEmbeddedClientSummonedUolPathToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            string normalizedToken = token
                .Trim()
                .Trim(ClientSummonedUolTokenTrimChars)
                .Replace('\\', '/');

            int skillPrefixIndex = FindClientSummonedUolEmbeddedSkillPrefixIndex(normalizedToken);
            if (skillPrefixIndex > 0)
            {
                normalizedToken = normalizedToken[skillPrefixIndex..];
            }

            return normalizedToken.Trim(ClientSummonedUolTokenTrimChars);
        }

        private static int FindClientSummonedUolEmbeddedSkillPrefixIndex(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return -1;
            }

            int skillPrefixIndex = -1;
            foreach (string prefix in ClientSummonedUolEmbeddedSkillPrefixes)
            {
                int index = value.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (index >= 0 && (skillPrefixIndex < 0 || index < skillPrefixIndex))
                {
                    skillPrefixIndex = index;
                }
            }

            int imagePathIndex = FindClientSummonedUolEmbeddedImagePathIndex(value);
            if (imagePathIndex >= 0 && (skillPrefixIndex < 0 || imagePathIndex < skillPrefixIndex))
            {
                skillPrefixIndex = imagePathIndex;
            }

            int mountedSkillPathIndex = FindClientSummonedUolEmbeddedMountedSkillPathIndex(value);
            if (mountedSkillPathIndex >= 0 && (skillPrefixIndex < 0 || mountedSkillPathIndex < skillPrefixIndex))
            {
                skillPrefixIndex = mountedSkillPathIndex;
            }

            return skillPrefixIndex;
        }

        private static int FindClientSummonedUolEmbeddedImagePathIndex(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return -1;
            }

            int searchStart = 0;
            while (searchStart < value.Length)
            {
                int imagePathSuffixIndex = value.IndexOf(
                    ClientSummonedUolEmbeddedImagePathSuffix,
                    searchStart,
                    StringComparison.OrdinalIgnoreCase);
                if (imagePathSuffixIndex < 0)
                {
                    return -1;
                }

                int imageNameEnd = imagePathSuffixIndex;
                int imageNameStart = imageNameEnd - 1;
                while (imageNameStart >= 0 && char.IsDigit(value[imageNameStart]))
                {
                    imageNameStart--;
                }

                imageNameStart++;
                if (imageNameStart < imageNameEnd
                    && int.TryParse(value.Substring(imageNameStart, imageNameEnd - imageNameStart), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    return imageNameStart;
                }

                searchStart = imagePathSuffixIndex + ClientSummonedUolEmbeddedImagePathSuffix.Length;
            }

            return -1;
        }

        private static int FindClientSummonedUolEmbeddedMountedSkillPathIndex(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return -1;
            }

            int searchStart = 0;
            while (searchStart < value.Length)
            {
                int mountedSkillPathIndex = value.IndexOf(
                    ClientSummonedUolEmbeddedMountedSkillPathPrefix,
                    searchStart,
                    StringComparison.OrdinalIgnoreCase);
                if (mountedSkillPathIndex < 0)
                {
                    return -1;
                }

                int skillIdStart = mountedSkillPathIndex + ClientSummonedUolEmbeddedMountedSkillPathPrefix.Length;
                int skillIdEnd = skillIdStart;
                while (skillIdEnd < value.Length && char.IsDigit(value[skillIdEnd]))
                {
                    skillIdEnd++;
                }

                if (skillIdEnd > skillIdStart
                    && int.TryParse(value.Substring(skillIdStart, skillIdEnd - skillIdStart), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    return mountedSkillPathIndex;
                }

                searchStart = mountedSkillPathIndex + ClientSummonedUolEmbeddedMountedSkillPathPrefix.Length;
            }

            return -1;
        }

        private static IEnumerable<int> EnumerateLinkedSkillIdsFromChildNames(WzImageProperty property)
        {
            if (property?.WzProperties == null)
            {
                yield break;
            }

            foreach (WzImageProperty child in property.WzProperties)
            {
                if (child != null && TryParseRequiredSkillId(child.Name, out int linkedSkillId))
                {
                    yield return linkedSkillId;
                }
            }
        }

        private static IEnumerable<int> EnumerateLinkedSkillIdsFromChildNameTokens(WzImageProperty property)
        {
            if (property?.WzProperties == null)
            {
                yield break;
            }

            var yieldedSkillIds = new HashSet<int>();
            foreach (WzImageProperty child in property.WzProperties)
            {
                if (child == null
                    || string.IsNullOrWhiteSpace(child.Name)
                    || TryParseRequiredSkillId(child.Name, out _))
                {
                    continue;
                }

                foreach (int linkedSkillId in EnumerateClientSummonedUolFallbackSkillIdsFromValue(
                             child.Name,
                             contextSkillId: 0))
                {
                    if (LooksLikeClientSummonedUolInferredSkillId(linkedSkillId)
                        && yieldedSkillIds.Add(linkedSkillId))
                    {
                        yield return linkedSkillId;
                    }
                }
            }
        }

        private static IEnumerable<int> EnumerateLinkedSkillIdsFromInfoLinkLeaves(WzImageProperty infoProperty)
        {
            foreach (string leafName in new[] { "requireSkill", "affectedSkill", "dummyOf" })
            {
                foreach (int linkedSkillId in ParseLinkedSkillIds(infoProperty?[leafName]))
                {
                    yield return linkedSkillId;
                }
            }
        }

        private static IEnumerable<int> EnumerateLinkedSkillIdsFromNestedClientSummonedUolInfoLeaves(
            WzImageProperty infoProperty,
            string[] resolvedPathParts)
        {
            if (infoProperty == null)
            {
                yield break;
            }

            var yieldedSkillIds = new HashSet<int>();
            foreach ((WzImageProperty Property, string RelativePath) nestedLeaf in EnumerateClientSummonedUolHeuristicInfoLeaves(
                         infoProperty,
                         relativePathPrefix: "info",
                         depthRemaining: ClientSummonedUolHeuristicInfoTraversalDepth))
            {
                if (!LooksLikeClientSummonedUolHeuristicOwnerPath(nestedLeaf.RelativePath))
                {
                    continue;
                }

                string[] nestedPathParts = BuildResolvedClientSummonedUolNestedPathParts(
                    resolvedPathParts,
                    nestedLeaf.RelativePath);
                foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromClientSummonedUolValueProperty(
                             nestedLeaf.Property,
                             nestedPathParts))
                {
                    if (linkedSkillId > 0 && yieldedSkillIds.Add(linkedSkillId))
                    {
                        yield return linkedSkillId;
                    }
                }
            }

            foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromNestedClientSummonedUolInfoBranchChildNames(
                         infoProperty,
                         resolvedPathParts))
            {
                if (linkedSkillId > 0 && yieldedSkillIds.Add(linkedSkillId))
                {
                    yield return linkedSkillId;
                }
            }
        }

        private static IEnumerable<int> EnumerateLinkedSkillIdsFromNestedClientSummonedUolInfoBranchChildNames(
            WzImageProperty infoProperty,
            string[] resolvedPathParts)
        {
            if (infoProperty == null)
            {
                yield break;
            }

            int contextSkillId = ResolveClientSummonedUolFallbackContextSkillId(resolvedPathParts);
            var yieldedSkillIds = new HashSet<int>();
            foreach ((WzImageProperty Branch, string RelativePath) nestedBranch in EnumerateClientSummonedUolHeuristicInfoBranches(
                         infoProperty,
                         relativePathPrefix: "info",
                         depthRemaining: ClientSummonedUolHeuristicInfoTraversalDepth))
            {
                if (!LooksLikeClientSummonedUolHeuristicOwnerPath(nestedBranch.RelativePath))
                {
                    continue;
                }

                foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromNestedClientSummonedUolInfoBranch(
                             nestedBranch.Branch,
                             contextSkillId))
                {
                    if (linkedSkillId > 0
                        && LooksLikeClientSummonedUolInferredSkillId(linkedSkillId)
                        && yieldedSkillIds.Add(linkedSkillId))
                    {
                        yield return linkedSkillId;
                    }
                }
            }
        }

        private static IEnumerable<int> EnumerateLinkedSkillIdsFromNestedClientSummonedUolInfoBranch(
            WzImageProperty branchProperty,
            int contextSkillId)
        {
            if (branchProperty == null)
            {
                yield break;
            }

            if (TryParseRequiredSkillId(branchProperty.Name, out int branchSkillId))
            {
                foreach (int candidateSkillId in ExpandClientSummonedUolHeuristicLinkedSkillIdCandidates(
                             branchSkillId,
                             contextSkillId))
                {
                    yield return candidateSkillId;
                }
            }

            foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromChildNames(branchProperty))
            {
                foreach (int candidateSkillId in ExpandClientSummonedUolHeuristicLinkedSkillIdCandidates(
                             linkedSkillId,
                             contextSkillId))
                {
                    yield return candidateSkillId;
                }
            }

            foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromChildNameTokens(branchProperty))
            {
                foreach (int candidateSkillId in ExpandClientSummonedUolHeuristicLinkedSkillIdCandidates(
                             linkedSkillId,
                             contextSkillId))
                {
                    yield return candidateSkillId;
                }
            }
        }

        private static IEnumerable<int> EnumerateLinkedSkillIdsFromReqOrPassiveBranchChildValues(
            WzImageProperty branchProperty,
            string[] branchPathParts)
        {
            if (branchProperty?.WzProperties == null)
            {
                yield break;
            }

            var yieldedSkillIds = new HashSet<int>();
            foreach (WzImageProperty child in branchProperty.WzProperties)
            {
                if (child == null || string.IsNullOrWhiteSpace(child.Name))
                {
                    continue;
                }

                if (TryParseRequiredSkillId(child.Name, out _))
                {
                    // req/* and psdSkill/* numeric children are authored as linked-skill ids in the name
                    // and usually use level/toggle values; avoid value-side contextual false positives.
                    continue;
                }

                string[] childPathParts = BuildResolvedClientSummonedUolChildPathParts(branchPathParts, child.Name);
                foreach (int linkedSkillId in EnumerateLinkedSkillIdsFromReqOrPassiveChildValueProperty(
                             child,
                             childPathParts))
                {
                    if (linkedSkillId > 0
                        && LooksLikeClientSummonedUolInferredSkillId(linkedSkillId)
                        && yieldedSkillIds.Add(linkedSkillId))
                    {
                        yield return linkedSkillId;
                    }
                }
            }
        }

        private static IEnumerable<int> EnumerateLinkedSkillIdsFromReqOrPassiveChildValueProperty(
            WzImageProperty childProperty,
            string[] resolvedPathParts)
        {
            if (childProperty == null)
            {
                yield break;
            }

            var yieldedSkillIds = new HashSet<int>();
            foreach (string valueText in EnumerateClientSummonedUolValueTexts(childProperty))
            {
                if (string.IsNullOrWhiteSpace(valueText))
                {
                    continue;
                }

                foreach (int parsedLinkedSkillId in ParseLinkedSkillIds(valueText))
                {
                    if (LooksLikeClientSummonedUolInferredSkillId(parsedLinkedSkillId)
                        && yieldedSkillIds.Add(parsedLinkedSkillId))
                    {
                        yield return parsedLinkedSkillId;
                    }
                }

                foreach (string pathToken in EnumerateClientSummonedUolPathTokensFromValue(valueText))
                {
                    if (string.IsNullOrWhiteSpace(pathToken))
                    {
                        continue;
                    }

                    string normalizedPath = NormalizeClientSummonedUolPathToken(pathToken, resolvedPathParts);
                    if (string.IsNullOrWhiteSpace(normalizedPath))
                    {
                        continue;
                    }

                    if (TryParseClientSummonedUolRootSkillId(normalizedPath, out int rootSkillId)
                        && LooksLikeClientSummonedUolInferredSkillId(rootSkillId)
                        && yieldedSkillIds.Add(rootSkillId))
                    {
                        yield return rootSkillId;
                    }

                    foreach (int linkedSkillId in EnumerateClientSummonedUolLinkedSkillIds(normalizedPath))
                    {
                        if (linkedSkillId > 0
                            && LooksLikeClientSummonedUolInferredSkillId(linkedSkillId)
                            && yieldedSkillIds.Add(linkedSkillId))
                        {
                            yield return linkedSkillId;
                        }
                    }
                }
            }
        }

        private static string[] BuildResolvedClientSummonedUolNestedPathParts(
            string[] resolvedPathParts,
            string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return resolvedPathParts;
            }

            string[] relativeParts = relativePath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (relativeParts.Length == 0)
            {
                return resolvedPathParts;
            }

            var nestedPathParts = new List<string>();
            if (resolvedPathParts != null && resolvedPathParts.Length > 0)
            {
                nestedPathParts.AddRange(resolvedPathParts.Take(Math.Max(resolvedPathParts.Length - 1, 0)));

                int relativePartStartIndex = 0;
                string resolvedLeafSegment = resolvedPathParts[^1];
                if (!string.IsNullOrWhiteSpace(resolvedLeafSegment)
                    && relativeParts[0].Equals(resolvedLeafSegment, StringComparison.OrdinalIgnoreCase))
                {
                    relativePartStartIndex = 1;
                }

                for (int i = relativePartStartIndex; i < relativeParts.Length; i++)
                {
                    nestedPathParts.Add(relativeParts[i]);
                }
            }

            if (nestedPathParts.Count == 0)
            {
                nestedPathParts.AddRange(relativeParts);
            }

            return nestedPathParts.ToArray();
        }

        private static string[] BuildResolvedClientSummonedUolChildPathParts(
            string[] resolvedPathParts,
            string childSegment)
        {
            if (string.IsNullOrWhiteSpace(childSegment))
            {
                return resolvedPathParts;
            }

            if (resolvedPathParts == null || resolvedPathParts.Length == 0)
            {
                return new[] { childSegment };
            }

            if (resolvedPathParts[^1].Equals(childSegment, StringComparison.OrdinalIgnoreCase))
            {
                return resolvedPathParts;
            }

            var childPathParts = new string[resolvedPathParts.Length + 1];
            Array.Copy(resolvedPathParts, childPathParts, resolvedPathParts.Length);
            childPathParts[^1] = childSegment;
            return childPathParts;
        }

        private static bool TryParseNormalizedSkillAnimationPath(
            string normalizedSkillAssetPath,
            out string imageName,
            out string[] propertySegments)
        {
            imageName = null;
            propertySegments = null;

            if (string.IsNullOrWhiteSpace(normalizedSkillAssetPath))
            {
                return false;
            }

            string[] parts = normalizedSkillAssetPath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3
                || !parts[0].Equals("Skill", StringComparison.OrdinalIgnoreCase)
                || !parts[1].EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            imageName = parts[1];
            propertySegments = parts.Skip(2).ToArray();
            return propertySegments.Length > 0;
        }

        private static bool TryNormalizeMountedSkillRootClientSummonedUolParts(
            string[] parts,
            out string normalizedPath)
        {
            normalizedPath = null;
            if (parts == null || parts.Length < 2)
            {
                return false;
            }

            int skillIdPartIndex = -1;
            if (parts[1].EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length >= 4
                    && parts[2].Equals("skill", StringComparison.OrdinalIgnoreCase)
                    && TryParseRequiredSkillId(parts[3], out _))
                {
                    skillIdPartIndex = 3;
                }
                else if (parts.Length >= 3 && TryParseRequiredSkillId(parts[2], out _))
                {
                    skillIdPartIndex = 2;
                }
                else
                {
                    return false;
                }
            }
            else if (TryParseRequiredSkillId(parts[1], out _)
                     && parts.Length >= 4
                     && parts[2].Equals("skill", StringComparison.OrdinalIgnoreCase)
                     && TryParseRequiredSkillId(parts[3], out _))
            {
                skillIdPartIndex = 3;
            }
            else if (TryParseRequiredSkillId(parts[1], out int mountedRootValue)
                     && parts.Length >= 3
                     && parts[1].Length <= 4
                     && TryParseRequiredSkillId(parts[2], out int childSkillId)
                     && childSkillId > mountedRootValue)
            {
                skillIdPartIndex = 2;
            }
            else if (TryParseRequiredSkillId(parts[1], out _))
            {
                skillIdPartIndex = 1;
            }
            else if (parts[1].Equals("skill", StringComparison.OrdinalIgnoreCase)
                     && parts.Length >= 3
                     && TryParseRequiredSkillId(parts[2], out _))
            {
                skillIdPartIndex = 2;
            }

            if (skillIdPartIndex < 0
                || !TryParseRequiredSkillId(parts[skillIdPartIndex], out int skillId))
            {
                return false;
            }

            string imageName = $"{skillId / 10000}.img";
            var normalizedParts = new List<string>(parts.Length + 2)
            {
                "Skill",
                imageName,
                "skill",
                skillId.ToString(CultureInfo.InvariantCulture)
            };

            for (int i = skillIdPartIndex + 1; i < parts.Length; i++)
            {
                normalizedParts.Add(parts[i]);
            }

            normalizedPath = string.Join("/", normalizedParts);
            return true;
        }

        private static string NormalizeClientSummonedUolPathSegments(IEnumerable<string> parts)
        {
            if (parts == null)
            {
                return null;
            }

            var normalizedParts = new List<string>();
            foreach (string part in parts)
            {
                if (string.IsNullOrWhiteSpace(part) || part.Equals(".", StringComparison.Ordinal))
                {
                    continue;
                }

                if (part.Equals("..", StringComparison.Ordinal))
                {
                    if (normalizedParts.Count > 0)
                    {
                        normalizedParts.RemoveAt(normalizedParts.Count - 1);
                    }

                    continue;
                }

                normalizedParts.Add(part);
            }

            if (normalizedParts.Count == 0
                || !normalizedParts[0].Equals("Skill", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return string.Join("/", normalizedParts);
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
                   || HasProperty(infoNode, "minionAttack")
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

        private SkillAnimation LoadItemBulletAnimationInternal(int itemId, int weaponItemId = 0, int weaponCode = 0)
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
            if (itemImage[itemText] is not WzSubProperty itemProperty)
            {
                return null;
            }

            WzImageProperty bulletProperty = ResolveItemBulletAnimationProperty(itemProperty, weaponItemId, weaponCode);
            if (bulletProperty == null)
            {
                return null;
            }

            SkillAnimation animation = LoadSkillAnimation(
                bulletProperty,
                "bullet",
                frameNode => LoadSkillFrame(frameNode, 60, false));
            return animation.Frames.Count > 0 ? animation : null;
        }

        internal static WzImageProperty ResolveItemBulletAnimationProperty(
            WzSubProperty itemProperty,
            int weaponItemId,
            int weaponCode = 0)
        {
            if (itemProperty?["bullet"] is not WzImageProperty bulletProperty)
            {
                return null;
            }

            if (bulletProperty is not WzSubProperty bulletContainer)
            {
                return bulletProperty;
            }

            foreach (string candidate in EnumerateItemBulletAnimationBranchKeys(weaponItemId, weaponCode))
            {
                if (bulletContainer[candidate] is WzImageProperty candidateProperty
                    && candidateProperty is not WzCanvasProperty
                    && HasItemBulletAnimationFrames(candidateProperty))
                {
                    return candidateProperty;
                }
            }

            if (HasItemBulletAnimationFrames(bulletProperty))
            {
                return bulletProperty;
            }

            foreach (WzImageProperty child in bulletContainer.WzProperties)
            {
                if (child is WzImageProperty candidateProperty
                    && HasItemBulletAnimationFrames(candidateProperty))
                {
                    return candidateProperty;
                }
            }

            return bulletProperty;
        }

        internal static IEnumerable<string> EnumerateItemBulletAnimationBranchKeys(int weaponItemId, int weaponCode = 0)
        {
            int derivedWeaponCode = ResolveItemBulletAnimationWeaponCodeFromWeaponItemId(weaponItemId);
            HashSet<string> emitted = new(StringComparer.OrdinalIgnoreCase);
            foreach (int candidate in new[]
                     {
                         weaponItemId,
                         weaponItemId / 10000,
                         weaponItemId / 1000000,
                         weaponCode,
                         derivedWeaponCode
                     })
            {
                if (candidate <= 0)
                {
                    continue;
                }

                foreach (string text in EnumerateItemBulletAnimationBranchKeyTextVariants(candidate))
                {
                    if (emitted.Add(text))
                    {
                        yield return text;
                    }
                }
            }
        }

        internal static int ResolveItemBulletAnimationWeaponCodeFromWeaponItemId(int weaponItemId)
        {
            if (weaponItemId <= 0)
            {
                return 0;
            }

            return Math.Abs(weaponItemId / 10000) % 100;
        }

        private static IEnumerable<string> EnumerateItemBulletAnimationBranchKeyTextVariants(int value)
        {
            if (value <= 0)
            {
                yield break;
            }

            string canonical = value.ToString(CultureInfo.InvariantCulture);
            yield return canonical;

            for (int width = 2; width <= 8; width++)
            {
                if (canonical.Length < width)
                {
                    yield return value.ToString($"D{width}", CultureInfo.InvariantCulture);
                }
            }
        }

        internal static bool HasItemBulletAnimationFrames(WzImageProperty bulletProperty)
        {
            if (bulletProperty == null)
            {
                return false;
            }

            if (bulletProperty is WzCanvasProperty)
            {
                return true;
            }

            return bulletProperty.WzProperties != null
                   && bulletProperty.WzProperties.Any(child =>
                       int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                       && child is WzCanvasProperty);
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
                canvas ??= frameNode.WzProperties?.OfType<WzCanvasProperty>().FirstOrDefault();
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

            // CAnimationDisplayer::LoadCanvas passes a0/a1 as independent optional InsertCanvas endpoints.
            // Preserve that client matrix here so lone authored a0 does not get re-expanded to an implicit 255 fade.
            int? authoredAlphaStart = TryResolveFrameInt(metadataNode, canvas, "a0", out int resolvedAlphaStart)
                ? resolvedAlphaStart
                : null;
            int? authoredAlphaEnd = TryResolveFrameInt(metadataNode, canvas, "a1", out int resolvedAlphaEnd)
                ? resolvedAlphaEnd
                : null;
            frame.HasAlphaStart = authoredAlphaStart.HasValue;
            frame.HasAlphaEnd = authoredAlphaEnd.HasValue;
            (frame.AlphaStart, frame.AlphaEnd) = ResolveClientInsertCanvasAlphaEndpoints(authoredAlphaStart, authoredAlphaEnd);
            frame.HasZoomStart = TryResolveFrameInt(metadataNode, canvas, "z0", out int resolvedZoomStart);
            frame.HasZoomEnd = TryResolveFrameInt(metadataNode, canvas, "z1", out int resolvedZoomEnd);
            frame.ZoomStart = frame.HasZoomStart ? resolvedZoomStart : 0;
            frame.ZoomEnd = frame.HasZoomEnd ? resolvedZoomEnd : 0;
            frame.RotationDegrees = ResolveFrameInt(metadataNode, canvas, "rotate");

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
                    || frameNode["z"] != null
                    || frameNode["a0"] != null
                    || frameNode["a1"] != null
                    || frameNode["z0"] != null
                    || frameNode["z1"] != null))
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

        private static bool TryResolveFrameInt(WzImageProperty metadataNode, WzCanvasProperty canvas, string propertyName, out int value)
        {
            if (metadataNode != null && metadataNode[propertyName] != null)
            {
                value = GetInt(metadataNode, propertyName);
                return true;
            }

            if (canvas != null && canvas[propertyName] != null)
            {
                value = GetInt(canvas, propertyName);
                return true;
            }

            value = default;
            return false;
        }

        internal static (int StartAlpha, int EndAlpha) ResolveClientInsertCanvasAlphaEndpoints(int? authoredAlphaStart, int? authoredAlphaEnd)
        {
            int startAlpha = authoredAlphaStart.HasValue
                ? Math.Clamp(authoredAlphaStart.Value, 0, 255)
                : 255;
            int endAlpha = authoredAlphaEnd.HasValue
                ? Math.Clamp(authoredAlphaEnd.Value, 0, 255)
                : 255;

            if (authoredAlphaStart.HasValue && !authoredAlphaEnd.HasValue)
            {
                endAlpha = startAlpha;
            }

            return (startAlpha, endAlpha);
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

            if (node.WzProperties == null)
            {
                return false;
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

                if (child.WzProperties == null)
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

        private ProjectileData LoadProjectile(
            int skillId,
            WzImageProperty ballNode,
            WzImageProperty skillNode,
            string explicitBallUolPath = null,
            string explicitFlipBallUolPath = null,
            IReadOnlyDictionary<int, string> explicitCharacterLevelBallUolPaths = null,
            IReadOnlyDictionary<int, string> explicitCharacterLevelFlipBallUolPaths = null,
            IReadOnlyDictionary<int, string> explicitLevelBallUolPaths = null,
            IReadOnlyDictionary<int, string> explicitLevelFlipBallUolPaths = null)
        {
            var projectile = new ProjectileData
            {
                SkillId = skillId,
                BallUolPath = !string.IsNullOrWhiteSpace(explicitBallUolPath)
                    ? explicitBallUolPath
                    : NormalizeSkillAssetPath(ballNode),
                FlipBallUolPath = !string.IsNullOrWhiteSpace(explicitFlipBallUolPath)
                    ? explicitFlipBallUolPath
                    : NormalizeSkillAssetPath(skillNode?["flipBall"])
            };

            // Load ball animation
            projectile.Animation = LoadSkillAnimation(ballNode, "ball");
            projectile.FlipAnimation = LoadSkillAnimation(skillNode?["flipBall"], "flipBall");
            projectile.AnimationPath = NormalizeSkillAssetPath(ballNode) ?? projectile.BallUolPath;
            projectile.VariantAnimations = LoadSummonIndexedAnimations(ballNode, "ball");
            projectile.VariantAnimationPaths = LoadSummonIndexedAnimationPaths(ballNode);
            if (projectile.Animation?.Frames.Count <= 0)
            {
                projectile.Animation = !string.IsNullOrWhiteSpace(projectile.BallUolPath)
                    ? LoadSkillAnimationByNormalizedPath(projectile.BallUolPath)
                    : null;
            }

            if (projectile.Animation?.Frames.Count <= 0)
            {
                projectile.Animation = projectile.ResolveAnimationVariant(level: 1);
                projectile.AnimationPath = projectile.VariantAnimationPaths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path))
                    ?? projectile.AnimationPath
                    ?? projectile.BallUolPath;
            }

            if (projectile.FlipAnimation?.Frames.Count <= 0
                && !string.IsNullOrWhiteSpace(projectile.FlipBallUolPath))
            {
                projectile.FlipAnimation = LoadSkillAnimationByNormalizedPath(projectile.FlipBallUolPath);
            }

            PopulateProjectileCharacterLevelVariants(projectile, skillNode?["CharLevel"]);
            PopulateProjectileLevelVariants(projectile, skillNode?["level"]);
            ApplyClientSkillAssetUolVariantPaths(
                projectile.CharacterLevelBallUolPaths,
                explicitCharacterLevelBallUolPaths);
            ApplyClientSkillAssetUolVariantPaths(
                projectile.CharacterLevelFlipBallUolPaths,
                explicitCharacterLevelFlipBallUolPaths);
            ApplyClientSkillAssetUolVariantPaths(
                projectile.LevelBallUolPaths,
                explicitLevelBallUolPaths);
            ApplyClientSkillAssetUolVariantPaths(
                projectile.LevelFlipBallUolPaths,
                explicitLevelFlipBallUolPaths);

            // Load hit animation
            var hitNode = ballNode?["hit"] ?? skillNode["hit"];
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
                projectile.CharacterLevelBallUolPaths[requiredLevel] = NormalizeSkillAssetPath(ballVariantNode);
                projectile.CharacterLevelFlipBallUolPaths[requiredLevel] = NormalizeSkillAssetPath(child["flipBall"]);
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
                projectile.LevelBallUolPaths[skillLevel] = NormalizeSkillAssetPath(ballVariantNode);
                projectile.LevelFlipBallUolPaths[skillLevel] = NormalizeSkillAssetPath(child["flipBall"]);
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

        private static void ApplyClientSkillAssetUolVariantPaths(
            IDictionary<int, string> targetPaths,
            IReadOnlyDictionary<int, string> sourcePaths)
        {
            if (targetPaths == null || sourcePaths == null || sourcePaths.Count == 0)
            {
                return;
            }

            foreach ((int variantLevel, string normalizedPath) in sourcePaths)
            {
                if (variantLevel <= 0 || string.IsNullOrWhiteSpace(normalizedPath))
                {
                    continue;
                }

                targetPaths[variantLevel] = normalizedPath;
            }
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
                    skill.IconDisabledTexture = texture;
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

                var skill = LoadSkillMetadata(skillId);
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
                .Where(skill => IsExplicitSwallowFamilyRoot(skill, skillsById))
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
                    if (linkedSkillId > 0
                        && skillsById.TryGetValue(linkedSkillId, out SkillData linkedSkill)
                        && linkedSkill?.IsSwallowSkill == true)
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

        private static bool IsExplicitSwallowFamilyRoot(
            SkillData skill,
            IReadOnlyDictionary<int, SkillData> skillsById)
        {
            if (skill == null
                || skill.DummySkillParents == null
                || skill.DummySkillParents.Length == 0)
            {
                return false;
            }

            if (!HasAuthoredSwallowFamilySignal(skill, skillsById))
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

        private static bool HasAuthoredSwallowFamilySignal(
            SkillData skill,
            IReadOnlyDictionary<int, SkillData> skillsById)
        {
            if (skill?.DummySkillParents == null || skillsById == null)
            {
                return false;
            }

            if (skill.IsSwallowSkill)
            {
                return true;
            }

            foreach (int linkedSkillId in skill.DummySkillParents)
            {
                if (linkedSkillId > 0
                    && skillsById.TryGetValue(linkedSkillId, out SkillData linkedSkill)
                    && linkedSkill?.IsSwallowSkill == true)
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

            return GetActionNamesFromActionNode(actionNode);
        }

        private static IReadOnlyList<string> GetMorphTemplateRequestedActionNames(
            WzImageProperty skillNode,
            IEnumerable<string> rootActionNames)
        {
            var actionNames = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddActionNames(actionNames, seen, rootActionNames);

            if (skillNode?["action"] != null && actionNames.Count == 0)
            {
                AddActionNames(actionNames, seen, GetActionNames(skillNode));
            }

            foreach (WzImageProperty actionNode in EnumerateNestedMorphActionNodes(skillNode))
            {
                AddActionNames(actionNames, seen, GetActionNamesFromActionNode(actionNode));
            }

            return actionNames;
        }

        private static IEnumerable<WzImageProperty> EnumerateNestedMorphActionNodes(WzImageProperty skillNode)
        {
            if (skillNode?.WzProperties == null)
            {
                yield break;
            }

            var stack = new Stack<WzImageProperty>(
                skillNode.WzProperties
                    .Where(static child => child != null)
                    .Reverse());

            while (stack.Count > 0)
            {
                WzImageProperty node = stack.Pop();
                if (string.Equals(node.Name, "action", StringComparison.OrdinalIgnoreCase))
                {
                    yield return node;
                    continue;
                }

                foreach (WzImageProperty child in node.WzProperties?
                             .Where(static child => child != null)
                             .Reverse()
                         ?? Enumerable.Empty<WzImageProperty>())
                {
                    stack.Push(child);
                }
            }
        }

        internal static IReadOnlyList<string> GetMorphTemplateRequestedActionNamesForTesting(
            WzImageProperty skillNode,
            IEnumerable<string> rootActionNames = null)
        {
            return GetMorphTemplateRequestedActionNames(skillNode, rootActionNames ?? Array.Empty<string>());
        }

        private static IReadOnlyList<string> GetActionNamesFromActionNode(WzImageProperty actionNode)
        {
            if (actionNode == null)
            {
                return Array.Empty<string>();
            }

            var actionNames = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (actionNode is WzStringProperty directActionProperty)
            {
                AddActionName(actionNames, seen, directActionProperty.Value);
                return actionNames;
            }

            AddActionNames(actionNames, seen, new[]
            {
                GetString(actionNode, "0"),
                GetString(actionNode, "action")
            });

            foreach (WzImageProperty property in actionNode.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            {
                AddActionName(actionNames, seen, GetPropertyStringValue(property));
            }

            return actionNames;
        }

        private static void AddActionNames(
            ICollection<string> actionNames,
            ISet<string> seen,
            IEnumerable<string> candidates)
        {
            if (candidates == null)
            {
                return;
            }

            foreach (string candidate in candidates)
            {
                AddActionName(actionNames, seen, candidate);
            }
        }

        private static void AddActionName(ICollection<string> actionNames, ISet<string> seen, string actionName)
        {
            if (actionNames == null || seen == null || string.IsNullOrWhiteSpace(actionName))
            {
                return;
            }

            string normalizedActionName = actionName.Trim();
            if (seen.Add(normalizedActionName))
            {
                actionNames.Add(normalizedActionName);
            }
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
            levelData.Craft = GetInt(node, "incCraft", 0, level);
            levelData.ACC = GetInt(node, "acc", 0, level);
            levelData.EVA = GetInt(node, "eva", 0, level);
            levelData.Speed = GetInt(node, "speed", 0, level);
            levelData.Jump = GetInt(node, "jump", 0, level);

            levelData.HP = GetInt(node, "hp", 0, level);
            levelData.MP = GetInt(node, "mp", 0, level);

            levelData.Prop = GetInt(node, "prop", 0, level);
            levelData.SubProp = GetInt(node, "subProp", 0, level);
            levelData.X = GetInt(node, "x", 0, level);
            levelData.Y = GetInt(node, "y", 0, level);
            levelData.Z = GetInt(node, "z", 0, level);
            levelData.U = GetInt(node, "u", 0, level);
            levelData.V = GetInt(node, "v", 0, level);
            levelData.W = GetInt(node, "w", 0, level);

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
            levelData.DamageReductionRate = PreferPrimaryStat(levelData.DamageReductionRate, GetInt(node, "PVPdamage", 0, level));
            levelData.BossDamageRate = GetInt(node, "bdR", 0, level);
            levelData.IgnoreDefenseRate = PreferPrimaryStat(
                GetInt(node, "ignoreMobpdpR", 0, level),
                GetInt(node, "ignoreMobDamR", 0, level));
            // Keep previously resolved description-backed aliases (for example `ar`/`er`)
            // when the skill node omits explicit `accR`/`evaR` percent fields.
            levelData.DefensePercent = PreferPrimaryStat(levelData.DefensePercent, GetInt(node, "pddR", 0, level));
            levelData.MagicDefensePercent = PreferPrimaryStat(levelData.MagicDefensePercent, GetInt(node, "mddR", 0, level));
            levelData.AccuracyPercent = PreferPrimaryStat(levelData.AccuracyPercent, GetInt(node, "accR", 0, level));
            levelData.AvoidabilityPercent = PreferPrimaryStat(levelData.AvoidabilityPercent, GetInt(node, "evaR", 0, level));

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

            int accuracyRateAlias = GetInt(node, "ar", 0, level);
            if (ResolveDescriptionBackedNamedPercentAlias(skill, "#ar", accuracyRateAlias, "accuracy") > 0)
            {
                levelData.AccuracyPercent = PreferPrimaryStat(levelData.AccuracyPercent, accuracyRateAlias);
            }
            else
            {
                levelData.ACC = PreferPrimaryStat(levelData.ACC, accuracyRateAlias);
            }

            levelData.ACC = PreferPrimaryStat(levelData.ACC, GetInt(node, "accX", 0, level));
            levelData.PAD = PreferPrimaryStat(levelData.PAD, GetInt(node, "padX", 0, level));
            levelData.MAD = PreferPrimaryStat(levelData.MAD, GetInt(node, "madX", 0, level));
            levelData.PDD = PreferPrimaryStat(levelData.PDD, GetInt(node, "pddX", 0, level));
            levelData.MDD = PreferPrimaryStat(levelData.MDD, GetInt(node, "mddX", 0, level));
            levelData.STR = PreferPrimaryStat(levelData.STR, GetInt(node, "strX", 0, level));
            levelData.DEX = PreferPrimaryStat(levelData.DEX, GetInt(node, "dexX", 0, level));
            levelData.INT = PreferPrimaryStat(levelData.INT, GetInt(node, "intX", 0, level));
            levelData.LUK = PreferPrimaryStat(levelData.LUK, GetInt(node, "lukX", 0, level));
            levelData.Craft = PreferPrimaryStat(levelData.Craft, GetInt(node, "craft", 0, level));
            levelData.Craft = PreferPrimaryStat(levelData.Craft, GetInt(node, "craftX", 0, level));
            levelData.Craft = PreferPrimaryStat(levelData.Craft, GetInt(node, "incCraft", 0, level));
            int avoidabilityRateAlias = GetInt(node, "er", 0, level);
            if (ResolveDescriptionBackedNamedPercentAlias(
                    skill,
                    "#er",
                    avoidabilityRateAlias,
                    "avoidability",
                    "dodge chance",
                    "enemy attack avoidance rate",
                    "base avoidability") > 0)
            {
                levelData.AvoidabilityPercent = PreferPrimaryStat(levelData.AvoidabilityPercent, avoidabilityRateAlias);
            }
            else
            {
                levelData.EVA = PreferPrimaryStat(levelData.EVA, avoidabilityRateAlias);
            }

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
            levelData.Speed = PreferPrimaryStat(levelData.Speed, GetInt(node, "psdSpeed", 0, level));
            levelData.SpeedMax = PreferPrimaryStat(levelData.SpeedMax, GetInt(node, "speedMax", 0, level));
            levelData.Jump = PreferPrimaryStat(levelData.Jump, GetInt(node, "psdJump", 0, level));
            levelData.AttackPercent = PreferPrimaryStat(levelData.AttackPercent, GetInt(node, "padR", 0, level));
            levelData.AttackPercent = PreferPrimaryStat(levelData.AttackPercent, GetInt(node, "padRate", 0, level));
            levelData.AttackPercent = PreferPrimaryStat(levelData.AttackPercent, GetInt(node, "indiePadR", 0, level));
            levelData.MagicAttackPercent = PreferPrimaryStat(levelData.MagicAttackPercent, GetInt(node, "madR", 0, level));
            levelData.MagicAttackPercent = PreferPrimaryStat(levelData.MagicAttackPercent, GetInt(node, "madRate", 0, level));
            levelData.MagicAttackPercent = PreferPrimaryStat(levelData.MagicAttackPercent, GetInt(node, "indieMadR", 0, level));
            levelData.SpeedPercent = PreferPrimaryStat(levelData.SpeedPercent, GetInt(node, "speedR", 0, level));
            levelData.SpeedPercent = PreferPrimaryStat(levelData.SpeedPercent, GetInt(node, "speedRate", 0, level));
            levelData.SpeedPercent = PreferPrimaryStat(levelData.SpeedPercent, GetInt(node, "indieSpeedR", 0, level));
            levelData.StrengthPercent = PreferPrimaryStat(levelData.StrengthPercent, GetInt(node, "strR", 0, level));
            levelData.StrengthPercent = PreferPrimaryStat(levelData.StrengthPercent, GetInt(node, "indieStrR", 0, level));
            levelData.DexterityPercent = PreferPrimaryStat(levelData.DexterityPercent, GetInt(node, "dexR", 0, level));
            levelData.DexterityPercent = PreferPrimaryStat(levelData.DexterityPercent, GetInt(node, "indieDexR", 0, level));
            levelData.IntelligencePercent = PreferPrimaryStat(levelData.IntelligencePercent, GetInt(node, "intR", 0, level));
            levelData.IntelligencePercent = PreferPrimaryStat(levelData.IntelligencePercent, GetInt(node, "indieIntR", 0, level));
            levelData.LuckPercent = PreferPrimaryStat(levelData.LuckPercent, GetInt(node, "lukR", 0, level));
            levelData.LuckPercent = PreferPrimaryStat(levelData.LuckPercent, GetInt(node, "indieLukR", 0, level));
            levelData.AllStatPercent = PreferPrimaryStat(levelData.AllStatPercent, GetInt(node, "allStatR", 0, level));
            levelData.AllStatPercent = PreferPrimaryStat(levelData.AllStatPercent, GetInt(node, "indieAllStatR", 0, level));
            levelData.StrengthToDexterityPercent = GetInt(node, "str2dex", 0, level);
            levelData.DexterityToStrengthPercent = GetInt(node, "dex2str", 0, level);
            levelData.IntelligenceToLuckPercent = GetInt(node, "int2luk", 0, level);
            levelData.LuckToDexterityPercent = GetInt(node, "luk2dex", 0, level);
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
            levelData.DefensePercent = PreferPrimaryStat(levelData.DefensePercent, GetInt(node, "pddR", 0, level));
            levelData.MagicDefensePercent = PreferPrimaryStat(levelData.MagicDefensePercent, GetInt(node, "mddR", 0, level));
            levelData.AccuracyPercent = PreferPrimaryStat(levelData.AccuracyPercent, GetInt(node, "accR", 0, level));
            levelData.AvoidabilityPercent = PreferPrimaryStat(levelData.AvoidabilityPercent, GetInt(node, "evaR", 0, level));
            levelData.AllStat = GetInt(node, "indieAllStat", 0, level);
            levelData.AbnormalStatusResistance = PreferPrimaryStat(GetInt(node, "asrR", 0, level), GetInt(node, "indieAsrR", 0, level));
            levelData.ElementalResistance = PreferPrimaryStat(GetInt(node, "terR", 0, level), GetInt(node, "indieTerR", 0, level));
            levelData.CriticalDamageMin = PreferPrimaryStat(levelData.CriticalDamageMin, GetInt(node, "criticalDamageMin", 0, level));
            levelData.CriticalDamageMax = PreferPrimaryStat(levelData.CriticalDamageMax, GetInt(node, "criticalDamageMax", 0, level));
            levelData.DamageReductionRate = PreferPrimaryStat(levelData.DamageReductionRate, GetInt(node, "indieDamR", 0, level));
            levelData.DamageReductionRate = PreferPrimaryStat(levelData.DamageReductionRate, GetInt(node, "PVPdamage", 0, level));
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

            ReadOnlySpan<(string PlaceholderToken, int AliasValue)> placeholders =
            [
                ("#x", xValue),
                ("#y", yValue),
                ("#z", zValue),
                ("#u", uValue),
                ("#v", vValue),
                ("#w", wValue)
            ];

            levelData.PAD = ApplyDescriptionBackedLabelAliases(
                levelData.PAD,
                normalizedSurface,
                requirePercentSuffix: false,
                placeholders,
                "weapon att",
                "weapon attack",
                "weapon and magic att",
                "weapon and magic attack",
                "weapon/magic att",
                "weapon/magic attack");
            levelData.MAD = ApplyDescriptionBackedLabelAliases(
                levelData.MAD,
                normalizedSurface,
                requirePercentSuffix: false,
                placeholders,
                "magic att",
                "magic attack",
                "weapon and magic att",
                "weapon and magic attack",
                "weapon/magic att",
                "weapon/magic attack");
            levelData.PAD = ApplyDescriptionBackedAliasValue(
                levelData.PAD,
                yValue,
                PlaceholderMatchesGenericAttackHint(normalizedSurface, "#y", requirePercentSuffix: false));
            levelData.PAD = ApplyDescriptionBackedAliasValue(
                levelData.PAD,
                zValue,
                PlaceholderMatchesGenericAttackHint(normalizedSurface, "#z", requirePercentSuffix: false));
            levelData.PAD = ApplyDescriptionBackedAliasValue(
                levelData.PAD,
                wValue,
                PlaceholderMatchesGenericAttackHint(normalizedSurface, "#w", requirePercentSuffix: false));
            levelData.PDD = ApplyDescriptionBackedLabelAliases(
                levelData.PDD,
                normalizedSurface,
                requirePercentSuffix: false,
                placeholders,
                "weapon def",
                "weapon defense");
            levelData.MDD = ApplyDescriptionBackedLabelAliases(
                levelData.MDD,
                normalizedSurface,
                requirePercentSuffix: false,
                placeholders,
                "magic def",
                "magic defense");
            levelData.STR = ApplyDescriptionBackedLabelAliases(
                levelData.STR,
                normalizedSurface,
                requirePercentSuffix: false,
                placeholders,
                "str",
                "strength");
            levelData.DEX = ApplyDescriptionBackedLabelAliases(
                levelData.DEX,
                normalizedSurface,
                requirePercentSuffix: false,
                placeholders,
                "dex",
                "dexterity");
            levelData.INT = ApplyDescriptionBackedLabelAliases(
                levelData.INT,
                normalizedSurface,
                requirePercentSuffix: false,
                placeholders,
                "int",
                "intelligence");
            levelData.LUK = ApplyDescriptionBackedLabelAliases(
                levelData.LUK,
                normalizedSurface,
                requirePercentSuffix: false,
                placeholders,
                "luk",
                "luck");
            levelData.AllStat = ApplyDescriptionBackedLabelAliases(
                levelData.AllStat,
                normalizedSurface,
                requirePercentSuffix: false,
                placeholders,
                "all stats",
                "all stat",
                "all attributes");
            levelData.ACC = ApplyDescriptionBackedLabelAliases(
                levelData.ACC,
                normalizedSurface,
                requirePercentSuffix: false,
                placeholders,
                "accuracy");
            levelData.EVA = ApplyDescriptionBackedLabelAliases(
                levelData.EVA,
                normalizedSurface,
                requirePercentSuffix: false,
                placeholders,
                "avoidability");
            foreach ((string placeholderToken, int aliasValue) in placeholders)
            {
                levelData.Speed = ApplyDescriptionBackedPositivePlayerAliasValue(
                    levelData.Speed,
                    aliasValue,
                    PlaceholderMatchesMovementSpeedHint(normalizedSurface, placeholderToken));
                levelData.SpeedMax = ApplyDescriptionBackedAliasValue(
                    levelData.SpeedMax,
                    aliasValue,
                    PlaceholderMatchesMaxMovementSpeedHint(normalizedSurface, placeholderToken));
            }
            levelData.Jump = ApplyDescriptionBackedLabelAliases(
                levelData.Jump,
                normalizedSurface,
                requirePercentSuffix: false,
                placeholders,
                "jump");
            levelData.CriticalRate = ApplyDescriptionBackedLabelAliases(
                levelData.CriticalRate,
                normalizedSurface,
                requirePercentSuffix: null,
                placeholders,
                "critical rate",
                "critical hit rate",
                "critical chance",
                "crit rate",
                "crit chance");
            levelData.MaxHPPercent = ApplyDescriptionBackedLabelAliases(
                levelData.MaxHPPercent,
                normalizedSurface,
                requirePercentSuffix: true,
                placeholders,
                "max hp",
                "maxhp",
                "maximum hp",
                "maximum health");
            levelData.MaxMPPercent = ApplyDescriptionBackedLabelAliases(
                levelData.MaxMPPercent,
                normalizedSurface,
                requirePercentSuffix: true,
                placeholders,
                "max mp",
                "maxmp",
                "maximum mp",
                "maximum mana");
            levelData.DefensePercent = ApplyDescriptionBackedLabelAliases(
                levelData.DefensePercent,
                normalizedSurface,
                requirePercentSuffix: true,
                placeholders,
                "weapon def",
                "weapon defense",
                "physical def",
                "physical defense");
            levelData.DefensePercent = ApplyDescriptionBackedAliasValue(
                levelData.DefensePercent,
                zValue,
                PlaceholderMatchesGenericDefenseHint(normalizedSurface, "#z", requirePercentSuffix: true));
            levelData.MagicDefensePercent = ApplyDescriptionBackedLabelAliases(
                levelData.MagicDefensePercent,
                normalizedSurface,
                requirePercentSuffix: true,
                placeholders,
                "magic def",
                "magic defense");
            levelData.AccuracyPercent = ApplyDescriptionBackedLabelAliases(
                levelData.AccuracyPercent,
                normalizedSurface,
                requirePercentSuffix: true,
                placeholders,
                "accuracy");
            levelData.AvoidabilityPercent = ApplyDescriptionBackedLabelAliases(
                levelData.AvoidabilityPercent,
                normalizedSurface,
                requirePercentSuffix: true,
                placeholders,
                "avoidability",
                "dodge chance",
                "enemy attack avoidance rate",
                "base avoidability");
        }

        private static int ApplyDescriptionBackedAliasValue(int target, int aliasValue, bool shouldApply)
        {
            if (!shouldApply || aliasValue == 0)
            {
                return target;
            }

            return PreferPrimaryStat(target, aliasValue);
        }

        private static int ApplyDescriptionBackedPositivePlayerAliasValue(int target, int aliasValue, bool shouldApply)
        {
            if (!shouldApply || aliasValue <= 0)
            {
                return target;
            }

            return target <= 0 ? aliasValue : target;
        }

        private static int ApplyDescriptionBackedLabelAliases(
            int target,
            string normalizedSurface,
            bool? requirePercentSuffix,
            ReadOnlySpan<(string PlaceholderToken, int AliasValue)> placeholders,
            params string[] labelTokens)
        {
            int resolved = target;
            foreach ((string placeholderToken, int aliasValue) in placeholders)
            {
                resolved = ApplyDescriptionBackedAliasValue(
                    resolved,
                    aliasValue,
                    PlaceholderMatchesHintLabel(
                        normalizedSurface,
                        placeholderToken,
                        requirePercentSuffix,
                        labelTokens));
            }

            return resolved;
        }

        internal static string NormalizeDescriptionBackedAliasSurface(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = string.Join(
                " ",
                value.Replace("\\r", " ", StringComparison.Ordinal)
                    .Replace("\\n", " ", StringComparison.Ordinal)
                    .ToLowerInvariant()
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

            return normalized
                .Replace("maxhp", "max hp", StringComparison.Ordinal)
                .Replace("maxmp", "max mp", StringComparison.Ordinal)
                .Replace("avoidablity", "avoidability", StringComparison.Ordinal)
                .Replace("accurary", "accuracy", StringComparison.Ordinal);
        }

        internal static bool PlaceholderMatchesHintLabel(
            string normalizedSurface,
            string placeholderToken,
            bool? requirePercentSuffix = null,
            params string[] labelTokens)
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

                if (requirePercentSuffix.HasValue
                    && PlaceholderHasPercentSuffix(normalizedSurface, placeholderIndex + placeholderToken.Length) != requirePercentSuffix.Value)
                {
                    searchIndex = placeholderIndex + placeholderToken.Length;
                    continue;
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

        internal static int ResolveDescriptionBackedPrimaryStatPercentAlias(
            SkillData skill,
            SkillLevelData levelData,
            params string[] labelTokens)
        {
            if (skill == null || levelData == null || labelTokens == null || labelTokens.Length == 0)
            {
                return 0;
            }

            string normalizedSurface = NormalizeDescriptionBackedAliasSurface(SkillDataTextSurface.GetDescriptionSurface(skill));
            if (string.IsNullOrWhiteSpace(normalizedSurface))
            {
                return 0;
            }

            ReadOnlySpan<(string PlaceholderToken, int AliasValue)> placeholders =
            [
                ("#x", levelData.X),
                ("#y", levelData.Y),
                ("#z", levelData.Z),
                ("#u", levelData.U),
                ("#v", levelData.V),
                ("#w", levelData.W)
            ];

            foreach ((string placeholderToken, int aliasValue) in placeholders)
            {
                if (aliasValue == 0)
                {
                    continue;
                }

                if (PlaceholderMatchesHintLabel(
                    normalizedSurface,
                    placeholderToken,
                    requirePercentSuffix: true,
                    labelTokens))
                {
                    return Math.Max(0, aliasValue);
                }
            }

            return 0;
        }

        internal static int ResolveDescriptionBackedNamedPercentAlias(
            SkillData skill,
            string placeholderToken,
            int aliasValue,
            params string[] labelTokens)
        {
            if (skill == null || aliasValue == 0)
            {
                return 0;
            }

            string normalizedSurface = NormalizeDescriptionBackedAliasSurface(SkillDataTextSurface.GetDescriptionSurface(skill));
            return PlaceholderMatchesHintLabel(
                    normalizedSurface,
                    placeholderToken,
                    requirePercentSuffix: true,
                    labelTokens)
                ? Math.Max(0, aliasValue)
                : 0;
        }

        private static bool PlaceholderMatchesGenericAttackHint(
            string normalizedSurface,
            string placeholderToken,
            bool requirePercentSuffix)
        {
            return PlaceholderMatchesGenericHint(
                normalizedSurface,
                placeholderToken,
                requirePercentSuffix,
                static context => (context.Contains("att", StringComparison.Ordinal)
                                   || context.Contains("attack", StringComparison.Ordinal))
                                  && !context.Contains("weapon att", StringComparison.Ordinal)
                                  && !context.Contains("weapon attack", StringComparison.Ordinal)
                                  && !context.Contains("magic att", StringComparison.Ordinal)
                                  && !context.Contains("magic attack", StringComparison.Ordinal)
                                  && !context.Contains("battle mode att", StringComparison.Ordinal)
                                  && !context.Contains("enemy att", StringComparison.Ordinal)
                                  && !context.Contains("attacks", StringComparison.Ordinal)
                                  && !context.Contains("attack count", StringComparison.Ordinal)
                                  && !context.Contains("number of attacks", StringComparison.Ordinal)
                                  && !context.Contains("final attack count", StringComparison.Ordinal));
        }

        private static bool PlaceholderMatchesGenericDefenseHint(
            string normalizedSurface,
            string placeholderToken,
            bool requirePercentSuffix)
        {
            return PlaceholderMatchesGenericHint(
                normalizedSurface,
                placeholderToken,
                requirePercentSuffix,
                static context => (context.Contains("def", StringComparison.Ordinal)
                                   || context.Contains("defense", StringComparison.Ordinal))
                                  && !context.Contains("weapon def", StringComparison.Ordinal)
                                  && !context.Contains("weapon defense", StringComparison.Ordinal)
                                  && !context.Contains("magic def", StringComparison.Ordinal)
                                  && !context.Contains("magic defense", StringComparison.Ordinal)
                                  && !context.Contains("battle mode def", StringComparison.Ordinal)
                                  && !context.Contains("enemy def", StringComparison.Ordinal));
        }

        private static bool PlaceholderMatchesGenericSpeedHint(string normalizedSurface, string placeholderToken)
        {
            return PlaceholderMatchesGenericHint(
                normalizedSurface,
                placeholderToken,
                requirePercentSuffix: false,
                static context => (context.Contains("movement speed", StringComparison.Ordinal)
                                   || context.Contains("speed", StringComparison.Ordinal))
                                  && !context.Contains("attack speed", StringComparison.Ordinal)
                                  && !context.Contains("max movement speed", StringComparison.Ordinal)
                                  && !context.Contains("maximum movement speed", StringComparison.Ordinal)
                                  && !context.Contains("max speed", StringComparison.Ordinal)
                                  && !context.Contains("speed max", StringComparison.Ordinal)
                                  && !context.Contains("weapon speed", StringComparison.Ordinal));
        }

        private static bool PlaceholderMatchesMovementSpeedHint(string normalizedSurface, string placeholderToken)
        {
            return PlaceholderMatchesGenericSpeedHint(normalizedSurface, placeholderToken)
                   && !PlaceholderMatchesMaxMovementSpeedHint(normalizedSurface, placeholderToken)
                   && !PlaceholderMatchesHintLabel(
                       normalizedSurface,
                       placeholderToken,
                       requirePercentSuffix: false,
                       "enemy movement speed");
        }

        private static bool PlaceholderMatchesMaxMovementSpeedHint(string normalizedSurface, string placeholderToken)
        {
            return PlaceholderMatchesHintLabel(
                normalizedSurface,
                placeholderToken,
                requirePercentSuffix: false,
                "max movement speed",
                "maximum movement speed",
                "speed max",
                "max speed");
        }

        private static bool PlaceholderMatchesGenericHint(
            string normalizedSurface,
            string placeholderToken,
            bool requirePercentSuffix,
            Func<string, bool> contextMatcher)
        {
            if (string.IsNullOrWhiteSpace(normalizedSurface)
                || string.IsNullOrWhiteSpace(placeholderToken)
                || contextMatcher == null)
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

                if (PlaceholderHasPercentSuffix(normalizedSurface, placeholderIndex + placeholderToken.Length) == requirePercentSuffix)
                {
                    int contextStart = Math.Max(0, placeholderIndex - 48);
                    int contextLength = placeholderIndex - contextStart + placeholderToken.Length;
                    string context = normalizedSurface.Substring(contextStart, contextLength);
                    if (contextMatcher(context))
                    {
                        return true;
                    }
                }

                searchIndex = placeholderIndex + placeholderToken.Length;
            }

            return false;
        }

        private static bool PlaceholderHasPercentSuffix(string normalizedSurface, int placeholderEndIndex)
        {
            if (string.IsNullOrWhiteSpace(normalizedSurface) || placeholderEndIndex < 0)
            {
                return false;
            }

            int index = placeholderEndIndex;
            while (index < normalizedSurface.Length && normalizedSurface[index] == ' ')
            {
                index++;
            }

            return index < normalizedSurface.Length && normalizedSurface[index] == '%';
        }

        internal static int ResolveDescriptionBackedAttackPercentAlias(SkillData skill, WzImageProperty node, int level)
        {
            int attackPercent = UsesEchoOfHeroAttackPercentAlias(skill, node)
                ? Math.Max(0, GetInt(node, "x", 0, level))
                : 0;

            string normalizedSurface = NormalizeDescriptionBackedAliasSurface(SkillDataTextSurface.GetDescriptionSurface(skill));
            attackPercent = PreferPrimaryStat(
                attackPercent,
                ResolveDescriptionBackedGenericAttackPercentAlias(normalizedSurface, "#x", GetInt(node, "x", 0, level)));
            attackPercent = PreferPrimaryStat(
                attackPercent,
                ResolveDescriptionBackedSharedWeaponMagicAttackPercentAlias(normalizedSurface, "#x", GetInt(node, "x", 0, level)));
            attackPercent = PreferPrimaryStat(
                attackPercent,
                ResolveDescriptionBackedGenericAttackPercentAlias(normalizedSurface, "#y", GetInt(node, "y", 0, level)));
            attackPercent = PreferPrimaryStat(
                attackPercent,
                ResolveDescriptionBackedSharedWeaponMagicAttackPercentAlias(normalizedSurface, "#y", GetInt(node, "y", 0, level)));
            attackPercent = PreferPrimaryStat(
                attackPercent,
                ResolveDescriptionBackedGenericAttackPercentAlias(normalizedSurface, "#z", GetInt(node, "z", 0, level)));
            attackPercent = PreferPrimaryStat(
                attackPercent,
                ResolveDescriptionBackedSharedWeaponMagicAttackPercentAlias(normalizedSurface, "#z", GetInt(node, "z", 0, level)));
            attackPercent = PreferPrimaryStat(
                attackPercent,
                ResolveDescriptionBackedSharedWeaponMagicAttackPercentAlias(normalizedSurface, "#u", GetInt(node, "u", 0, level)));
            attackPercent = PreferPrimaryStat(
                attackPercent,
                ResolveDescriptionBackedSharedWeaponMagicAttackPercentAlias(normalizedSurface, "#v", GetInt(node, "v", 0, level)));
            attackPercent = PreferPrimaryStat(
                attackPercent,
                ResolveDescriptionBackedGenericAttackPercentAlias(normalizedSurface, "#w", GetInt(node, "w", 0, level)));
            attackPercent = PreferPrimaryStat(
                attackPercent,
                ResolveDescriptionBackedSharedWeaponMagicAttackPercentAlias(normalizedSurface, "#w", GetInt(node, "w", 0, level)));
            return attackPercent;
        }

        internal static int ResolveDescriptionBackedMagicAttackPercentAlias(SkillData skill, WzImageProperty node, int level)
        {
            int magicAttackPercent = UsesEchoOfHeroAttackPercentAlias(skill, node)
                ? Math.Max(0, GetInt(node, "x", 0, level))
                : 0;

            string normalizedSurface = NormalizeDescriptionBackedAliasSurface(SkillDataTextSurface.GetDescriptionSurface(skill));
            magicAttackPercent = PreferPrimaryStat(
                magicAttackPercent,
                ResolveDescriptionBackedGenericMagicAttackPercentAlias(normalizedSurface, "#x", GetInt(node, "x", 0, level)));
            magicAttackPercent = PreferPrimaryStat(
                magicAttackPercent,
                ResolveDescriptionBackedGenericMagicAttackPercentAlias(normalizedSurface, "#y", GetInt(node, "y", 0, level)));
            magicAttackPercent = PreferPrimaryStat(
                magicAttackPercent,
                ResolveDescriptionBackedGenericMagicAttackPercentAlias(normalizedSurface, "#z", GetInt(node, "z", 0, level)));
            magicAttackPercent = PreferPrimaryStat(
                magicAttackPercent,
                ResolveDescriptionBackedGenericMagicAttackPercentAlias(normalizedSurface, "#u", GetInt(node, "u", 0, level)));
            magicAttackPercent = PreferPrimaryStat(
                magicAttackPercent,
                ResolveDescriptionBackedGenericMagicAttackPercentAlias(normalizedSurface, "#v", GetInt(node, "v", 0, level)));
            magicAttackPercent = PreferPrimaryStat(
                magicAttackPercent,
                ResolveDescriptionBackedGenericMagicAttackPercentAlias(normalizedSurface, "#w", GetInt(node, "w", 0, level)));
            return magicAttackPercent;
        }

        private static int ResolveDescriptionBackedGenericAttackPercentAlias(
            string normalizedSurface,
            string placeholderToken,
            int aliasValue)
        {
            return PlaceholderMatchesGenericAttackHint(normalizedSurface, placeholderToken, requirePercentSuffix: true)
                ? Math.Max(0, aliasValue)
                : 0;
        }

        private static int ResolveDescriptionBackedSharedWeaponMagicAttackPercentAlias(
            string normalizedSurface,
            string placeholderToken,
            int aliasValue)
        {
            return PlaceholderMatchesHintLabel(
                    normalizedSurface,
                    placeholderToken,
                    requirePercentSuffix: true,
                    "weapon and magic att",
                    "weapon and magic attack",
                    "weapon/magic att",
                    "weapon/magic attack")
                ? Math.Max(0, aliasValue)
                : 0;
        }

        internal static int ResolveDescriptionBackedGenericMagicAttackPercentAlias(
            string normalizedSurface,
            string placeholderToken,
            int aliasValue)
        {
            return PlaceholderMatchesHintLabel(
                    normalizedSurface,
                    placeholderToken,
                    requirePercentSuffix: true,
                    "magic att",
                    "magic attack")
                ? Math.Max(0, aliasValue)
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

        private static int ResolveMorphTemplateId(
            int skillId,
            WzImageProperty commonNode,
            WzImageProperty pvpCommonNode,
            WzImageProperty infoNode,
            IEnumerable<string> requestedActionNames)
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

            return ResolveFlagOnlyMorphAliasTemplateId(
                skillId,
                commonNode,
                pvpCommonNode,
                infoNode,
                requestedActionNames);
        }

        internal static int ResolveMorphTemplateIdForTesting(
            int skillId,
            WzImageProperty commonNode,
            WzImageProperty pvpCommonNode,
            WzImageProperty infoNode,
            IEnumerable<string> requestedActionNames = null)
        {
            return ResolveMorphTemplateId(
                skillId,
                commonNode,
                pvpCommonNode,
                infoNode,
                requestedActionNames);
        }

        private static int TryGetConcreteMorphTemplateId(WzImageProperty node, string name)
        {
            WzImageProperty child = node?[name];
            if (!TryGetMorphTemplateIdValue(child, out int morphTemplateId))
            {
                return 0;
            }

            return morphTemplateId > 1 ? morphTemplateId : 0;
        }

        private static bool TryGetMorphTemplateIdValue(WzImageProperty property, out int value)
        {
            switch (property)
            {
                case WzIntProperty intProperty:
                    value = intProperty.Value;
                    return true;
                case WzShortProperty shortProperty:
                    value = shortProperty.Value;
                    return true;
                case WzLongProperty longProperty:
                    value = (int)longProperty.Value;
                    return true;
                case WzStringProperty stringProperty:
                    return int.TryParse(
                        stringProperty.Value?.Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out value);
                default:
                    value = 0;
                    return false;
            }
        }

        private static int ResolveFlagOnlyMorphAliasTemplateId(
            int skillId,
            WzImageProperty commonNode,
            WzImageProperty pvpCommonNode,
            WzImageProperty infoNode,
            IEnumerable<string> requestedActionNames)
        {
            bool hasFlagOnlyMorphMetadata =
                HasFlagOnlyMorphMetadata(commonNode, "morph")
                || HasFlagOnlyMorphMetadata(pvpCommonNode, "morph")
                || HasFlagOnlyMorphMetadata(infoNode, "morph");

            return ResolveFlagOnlyMorphAliasTemplateId(
                skillId,
                hasFlagOnlyMorphMetadata,
                requestedActionNames,
                CharacterLoader.CanResolveMorphTemplate);
        }

        private static int ResolveFlagOnlyMorphAliasTemplateId(
            int skillId,
            bool hasFlagOnlyMorphMetadata,
            IEnumerable<string> requestedActionNames,
            Func<int, IReadOnlyList<string>, bool> canResolveMorphTemplate)
        {
            if (!hasFlagOnlyMorphMetadata || canResolveMorphTemplate == null)
            {
                return 0;
            }

            IReadOnlyList<string> normalizedActionNames = EnumerateDistinctActionNames(
                    requestedActionNames ?? Array.Empty<string>())
                .ToArray();

            foreach (int morphTemplateId in EnumerateFlagOnlyMorphTemplateCandidates(skillId))
            {
                if (canResolveMorphTemplate(morphTemplateId, normalizedActionNames))
                {
                    return morphTemplateId;
                }
            }

            return 0;
        }

        internal static int ResolveFlagOnlyMorphAliasTemplateIdForTesting(
            int skillId,
            bool hasFlagOnlyMorphMetadata,
            IEnumerable<string> requestedActionNames,
            Func<int, IReadOnlyList<string>, bool> canResolveMorphTemplate)
        {
            return ResolveFlagOnlyMorphAliasTemplateId(
                skillId,
                hasFlagOnlyMorphMetadata,
                requestedActionNames,
                canResolveMorphTemplate);
        }

        private static IEnumerable<int> EnumerateFlagOnlyMorphTemplateCandidates(int skillId)
        {
            if (skillId <= 0)
            {
                yield break;
            }

            var seen = new HashSet<int>();
            if (ClientFlagOnlyMorphTemplateOverridesBySkillId.TryGetValue(skillId, out int[] overrideTemplateIds)
                && overrideTemplateIds != null)
            {
                bool yieldedOverride = false;
                foreach (int overrideTemplateId in overrideTemplateIds)
                {
                    if (overrideTemplateId > 0 && seen.Add(overrideTemplateId))
                    {
                        yieldedOverride = true;
                        yield return overrideTemplateId;
                    }
                }

                // Keep checked non-suffix outlier remaps authoritative when present so
                // suffix/family heuristics cannot reorder known rows.
                if (yieldedOverride)
                {
                    yield break;
                }
            }

            int suffixTemplateId = skillId % 10000;
            foreach (int candidateTemplateId in EnumerateFlagOnlyMorphTemplateFamilyCandidates(suffixTemplateId))
            {
                if (candidateTemplateId > 0 && seen.Add(candidateTemplateId))
                {
                    yield return candidateTemplateId;
                }
            }

            foreach (int candidateTemplateId in EnumerateFlagOnlyMorphTemplateTrimmedRemapCandidates(suffixTemplateId))
            {
                if (candidateTemplateId > 0 && seen.Add(candidateTemplateId))
                {
                    yield return candidateTemplateId;
                }
            }
        }

        internal static IReadOnlyList<int> EnumerateFlagOnlyMorphTemplateCandidatesForTesting(int skillId)
        {
            return EnumerateFlagOnlyMorphTemplateCandidates(skillId).ToArray();
        }

        private static IEnumerable<int> EnumerateFlagOnlyMorphTemplateFamilyCandidates(int suffixTemplateId)
        {
            if (suffixTemplateId <= 0)
            {
                yield break;
            }

            yield return suffixTemplateId;

            if (suffixTemplateId >= 1000 && suffixTemplateId < 1200)
            {
                int pairedTemplateId = suffixTemplateId >= 1100
                    ? suffixTemplateId - 100
                    : suffixTemplateId + 100;
                if (pairedTemplateId > 0)
                {
                    yield return pairedTemplateId;
                }
            }

            int[] familyBases =
            {
                (suffixTemplateId / 10) * 10,
                (suffixTemplateId / 100) * 100,
                (suffixTemplateId / 1000) * 1000
            };

            foreach (int familyBase in familyBases)
            {
                if (familyBase > 0)
                {
                    yield return familyBase;
                }
            }
        }

        private static IEnumerable<int> EnumerateFlagOnlyMorphTemplateTrimmedRemapCandidates(int suffixTemplateId)
        {
            if (suffixTemplateId < 1000)
            {
                yield break;
            }

            // WZ-first recheck keeps non-suffix flag-only remap evidence on
            // `Skill/2002.img/skill/20020111/info/morph = 1` where the effective
            // morph template is `0111` (last three digits) rather than suffix `1111`.
            // Keep this as a late fallback after suffix/pair/family candidates.
            int trimmedTemplateId = suffixTemplateId % 1000;
            if (trimmedTemplateId > 0)
            {
                yield return trimmedTemplateId;
            }
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

            foreach (int infoRequiredSkillId in ParseLinkedSkillIds(infoNode?["requireSkill"]))
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
