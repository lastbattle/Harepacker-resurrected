using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Util;
using HaCreator.MapSimulator.Character.Skills;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HaCreator.MapSimulator.Loaders
{
    /// <summary>
    /// Loads skill data from Skill.wz and String.wz for displaying in the skill window
    /// </summary>
    public static class SkillDataLoader
    {
        public readonly struct RecommendedSkillEntry
        {
            public RecommendedSkillEntry(int spentSpThreshold, int skillId)
            {
                SpentSpThreshold = spentSpThreshold;
                SkillId = skillId;
            }

            public int SpentSpThreshold { get; }
            public int SkillId { get; }
        }

        /// <summary>
        /// Job ID to skill book image mapping
        /// Beginner: 000.img, Warriors: 100.img, etc.
        /// </summary>
        private static readonly Dictionary<int, string> JobToSkillImage = new Dictionary<int, string>
        {
            // Beginner
            { 0, "000.img" },
            // Explorer Warriors
            { 100, "100.img" }, { 110, "110.img" }, { 111, "111.img" }, { 112, "112.img" },
            { 120, "120.img" }, { 121, "121.img" }, { 122, "122.img" },
            { 130, "130.img" }, { 131, "131.img" }, { 132, "132.img" },
            // Explorer Mages
            { 200, "200.img" }, { 210, "210.img" }, { 211, "211.img" }, { 212, "212.img" },
            { 220, "220.img" }, { 221, "221.img" }, { 222, "222.img" },
            { 230, "230.img" }, { 231, "231.img" }, { 232, "232.img" },
            // Explorer Archers
            { 300, "300.img" }, { 310, "310.img" }, { 311, "311.img" }, { 312, "312.img" },
            { 320, "320.img" }, { 321, "321.img" }, { 322, "322.img" },
            // Explorer Thieves
            { 400, "400.img" }, { 410, "410.img" }, { 411, "411.img" }, { 412, "412.img" },
            { 420, "420.img" }, { 421, "421.img" }, { 422, "422.img" },
            // Explorer Pirates
            { 500, "500.img" }, { 510, "510.img" }, { 511, "511.img" }, { 512, "512.img" },
            { 520, "520.img" }, { 521, "521.img" }, { 522, "522.img" },
        };

        /// <summary>
        /// Load all skills for a specific job using Program.FindImage
        /// </summary>
        /// <param name="jobId">Job ID (0 = Beginner, 100 = Warrior, etc.)</param>
        /// <param name="device">Graphics device for texture creation</param>
        /// <returns>List of skill display data</returns>
        public static List<SkillDisplayData> LoadSkillsForJob(int jobId, GraphicsDevice device)
        {
            List<SkillDisplayData> skills = new List<SkillDisplayData>();

            // Get skill image name for this job
            string skillImageName = GetSkillImageName(jobId);
            if (string.IsNullOrEmpty(skillImageName))
            {
                Debug.WriteLine($"[SkillDataLoader] No skill image name for job {jobId}");
                return skills;
            }

            // Load skill image using Program.FindImage (works with both IDataSource and WzManager)
            WzImage skillImage = Program.FindImage("Skill", skillImageName);
            if (skillImage == null)
            {
                Debug.WriteLine($"[SkillDataLoader] Failed to load Skill/{skillImageName}");
                return skills;
            }

            Debug.WriteLine($"[SkillDataLoader] Loaded Skill/{skillImageName}");

            // Load string image for skill names
            WzImage stringImage = Program.FindImage("String", "Skill.img");

            // Get skill subproperty
            WzSubProperty skillProperty = (WzSubProperty)skillImage["skill"];
            if (skillProperty == null)
            {
                Debug.WriteLine($"[SkillDataLoader] No 'skill' property in {skillImageName}");
                return skills;
            }

            Debug.WriteLine($"[SkillDataLoader] Found {skillProperty.WzProperties.Count} skill entries");

            // Iterate through all skills in this job
            foreach (WzImageProperty prop in skillProperty.WzProperties)
            {
                if (prop is WzSubProperty skillEntry)
                {
                    try
                    {
                        SkillDisplayData skillData = LoadSingleSkill(skillEntry, stringImage, device);
                        if (skillData != null)
                        {
                            skills.Add(skillData);
                            Debug.WriteLine($"[SkillDataLoader] Loaded skill: {skillData.SkillName} (ID: {skillData.SkillId})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SkillDataLoader] Error loading skill {prop.Name}: {ex.Message}");
                    }
                }
            }

            Debug.WriteLine($"[SkillDataLoader] Total skills loaded for job {jobId}: {skills.Count}");
            return skills;
        }

        public static List<SkillDisplayData> LoadGuildSkills(GraphicsDevice device)
        {
            return LoadSkillsFromImageName("9100.img", device);
        }

        public static IReadOnlyList<RecommendedSkillEntry> LoadRecommendedSkillEntries(
            int jobId,
            IEnumerable<int> validSkillIds = null)
        {
            WzImage recommendSkillImage = Program.FindImage("Etc", "RecommendSkill.img");
            if (recommendSkillImage == null)
                return Array.Empty<RecommendedSkillEntry>();

            if (recommendSkillImage[jobId.ToString(CultureInfo.InvariantCulture)] is not WzSubProperty recommendProperty)
                return Array.Empty<RecommendedSkillEntry>();

            HashSet<int> validSkillIdSet = validSkillIds != null
                ? new HashSet<int>(validSkillIds)
                : null;

            if (!TryBuildRecommendedSkillEntries(recommendProperty, validSkillIdSet, out List<RecommendedSkillEntry> entries))
                return Array.Empty<RecommendedSkillEntry>();

            return entries;
        }

        /// <summary>
        /// Enumerate every numeric skill book image available in Skill.wz.
        /// </summary>
        public static IReadOnlyList<int> GetAvailableSkillBookJobIds(WzFile skillWzFile)
        {
            var fromFile = SkillLoader.EnumerateSkillBookJobIds(skillWzFile);
            if (fromFile.Count > 0)
                return fromFile;

            var result = new SortedSet<int>();

            if (Program.FindWzObject("Skill", string.Empty) is WzDirectory rootDirectory)
            {
                CollectSkillBookJobIds(rootDirectory, result);
            }

            if (result.Count == 0)
            {
                foreach (var directory in Program.GetDirectories("Skill"))
                {
                    CollectSkillBookJobIds(directory, result);
                }
            }

            return new List<int>(result);
        }

        public static bool SkillRootContainsSkill(int skillRootId, int skillId)
        {
            if (skillRootId < 0 || skillId <= 0)
                return false;

            WzImage skillImage = Program.FindImage("Skill", GetSkillImageName(skillRootId));
            if (skillImage == null)
                return false;

            return skillImage["skill"] is WzSubProperty skillProperty &&
                   skillProperty[skillId.ToString(CultureInfo.InvariantCulture)] != null;
        }

        private static void CollectSkillBookJobIds(WzDirectory directory, ISet<int> result)
        {
            if (directory == null)
                return;

            foreach (var image in directory.WzImages)
            {
                if (image == null)
                    continue;

                string name = System.IO.Path.GetFileNameWithoutExtension(image.Name);
                if (int.TryParse(name, out int jobId))
                {
                    result.Add(jobId);
                }
            }

            foreach (var subDirectory in directory.WzDirectories)
            {
                CollectSkillBookJobIds(subDirectory, result);
            }
        }

        /// <summary>
        /// Load all skills for a specific job (legacy overload for compatibility)
        /// </summary>
        public static List<SkillDisplayData> LoadSkillsForJob(
            int jobId, WzFile skillWzFile, WzFile stringWzFile, GraphicsDevice device)
        {
            // Use the new method that uses Program.FindImage
            return LoadSkillsForJob(jobId, device);
        }

        /// <summary>
        /// Load beginner skills specifically (job 0)
        /// </summary>
        public static List<SkillDisplayData> LoadBeginnerSkills(
            WzFile skillWzFile, WzFile stringWzFile, GraphicsDevice device)
        {
            return LoadSkillsForJob(0, device);
        }

        /// <summary>
        /// Load beginner skills (simplified overload)
        /// </summary>
        public static List<SkillDisplayData> LoadBeginnerSkills(GraphicsDevice device)
        {
            return LoadSkillsForJob(0, device);
        }

        /// <summary>
        /// Load skills from a WzImage directly (for when you already have the image loaded)
        /// </summary>
        public static List<SkillDisplayData> LoadSkillsFromImage(
            WzImage skillImage, WzImage stringImage, GraphicsDevice device)
        {
            List<SkillDisplayData> skills = new List<SkillDisplayData>();

            if (skillImage == null)
                return skills;

            // Get skill subproperty
            WzSubProperty skillProperty = (WzSubProperty)skillImage["skill"];
            if (skillProperty == null)
                return skills;

            // Iterate through all skills
            foreach (WzImageProperty prop in skillProperty.WzProperties)
            {
                if (prop is WzSubProperty skillEntry)
                {
                    try
                    {
                        SkillDisplayData skillData = LoadSingleSkill(
                            skillEntry, stringImage, device);
                        if (skillData != null)
                        {
                            skills.Add(skillData);
                        }
                    }
                    catch
                    {
                        // Skip skills that fail to load
                    }
                }
            }

            return skills;
        }

        private static List<SkillDisplayData> LoadSkillsFromImageName(string skillImageName, GraphicsDevice device)
        {
            List<SkillDisplayData> skills = new List<SkillDisplayData>();
            if (string.IsNullOrWhiteSpace(skillImageName))
                return skills;

            WzImage skillImage = Program.FindImage("Skill", skillImageName);
            if (skillImage == null)
                return skills;

            WzImage stringImage = Program.FindImage("String", "Skill.img");
            return LoadSkillsFromImage(skillImage, stringImage, device);
        }

        /// <summary>
        /// Load a single skill from WZ data
        /// </summary>
        private static SkillDisplayData LoadSingleSkill(
            WzSubProperty skillEntry, WzImage stringImage, GraphicsDevice device)
        {
            string skillIdStr = skillEntry.Name;
            if (!int.TryParse(skillIdStr, out int skillId))
                return null;

            // Get skill icon
            WzCanvasProperty iconProp = (WzCanvasProperty)skillEntry["icon"];
            if (iconProp == null)
                return null;

            System.Drawing.Bitmap iconBitmap = iconProp.GetLinkedWzCanvasBitmap();
            Texture2D iconTexture = iconBitmap?.ToTexture2DAndDispose(device);
            if (iconTexture == null)
                return null;

            // Get disabled icon if available
            Texture2D disabledIconTexture = null;
            WzCanvasProperty disabledIconProp = (WzCanvasProperty)skillEntry["iconDisabled"];
            if (disabledIconProp != null)
            {
                System.Drawing.Bitmap disabledBitmap = disabledIconProp.GetLinkedWzCanvasBitmap();
                disabledIconTexture = disabledBitmap?.ToTexture2DAndDispose(device);
            }

            // Get mouse over icon if available
            Texture2D mouseOverIconTexture = null;
            WzCanvasProperty mouseOverIconProp = (WzCanvasProperty)skillEntry["iconMouseOver"];
            if (mouseOverIconProp != null)
            {
                System.Drawing.Bitmap mouseOverBitmap = mouseOverIconProp.GetLinkedWzCanvasBitmap();
                mouseOverIconTexture = mouseOverBitmap?.ToTexture2DAndDispose(device);
            }

            // Get skill info
            WzSubProperty infoProperty = (WzSubProperty)skillEntry["info"];
            int maxLevel = 0;
            bool invisible = false;
            bool timeLimited = false;

            if (infoProperty != null)
            {
                WzIntProperty maxLevelProp = (WzIntProperty)infoProperty["maxLevel"];
                if (maxLevelProp != null)
                    maxLevel = maxLevelProp.Value;

                invisible = ResolveBooleanProperty(infoProperty, "invisible");
                timeLimited = ResolveBooleanProperty(infoProperty, "timeLimited");
            }

            invisible |= ResolveBooleanProperty(skillEntry, "invisible");
            timeLimited |= ResolveBooleanProperty(skillEntry, "timeLimited");

            // Get level info for max level determination
            WzSubProperty levelProperty = (WzSubProperty)skillEntry["level"];
            if (levelProperty != null && maxLevel == 0)
            {
                maxLevel = levelProperty.WzProperties.Count;
            }

            // Get skill name and description from String.wz
            string skillName = $"Skill {skillId}";
            string description = "";
            string formattedDescription = string.Empty;
            Dictionary<int, string> levelDescriptions = null;
            Dictionary<int, string> formattedLevelDescriptions = null;

            if (stringImage != null)
            {
                WzSubProperty stringEntry = (WzSubProperty)stringImage[skillIdStr];
                if (stringEntry != null)
                {
                    WzStringProperty nameProp = (WzStringProperty)stringEntry["name"];
                    if (nameProp != null)
                        skillName = nameProp.Value;

                    WzStringProperty descProp = (WzStringProperty)stringEntry["desc"];
                    if (descProp != null)
                        description = descProp.Value;

                    if (string.IsNullOrWhiteSpace(description) && stringEntry["pdesc"] is WzStringProperty passiveDescProp)
                        description = passiveDescProp.Value;

                    levelDescriptions = BuildLevelDescriptions(skillEntry, stringEntry, Math.Max(1, maxLevel), preserveFormatting: false);
                    formattedLevelDescriptions = BuildLevelDescriptions(skillEntry, stringEntry, Math.Max(1, maxLevel), preserveFormatting: true);
                }
            }

            int requiredCharacterLevel = ResolveRequiredCharacterLevel(skillEntry);
            List<SkillRequirementDisplayData> requiredSkills = BuildRequiredSkills(skillEntry, stringImage, device);
            ResolveRequiredSkill(requiredSkills, out int requiredSkillId, out int requiredSkillLevel);
            bool hasExplicitRequirements = requiredSkills.Count > 0;
            formattedDescription = NormalizeSkillDescriptionForTooltip(description, hasExplicitRequirements, preserveFormatting: true);
            description = NormalizeSkillDescriptionForTooltip(description, hasExplicitRequirements);

            var displayData = new SkillDisplayData
            {
                SkillId = skillId,
                IconTexture = iconTexture,
                IconDisabledTexture = disabledIconTexture,
                IconMouseOverTexture = mouseOverIconTexture,
                SkillName = skillName,
                Description = description,
                FormattedDescription = formattedDescription,
                IsInvisible = invisible,
                IsTimeLimited = timeLimited,
                CurrentLevel = 0,
                MaxLevel = Math.Max(1, maxLevel),
                RequiredCharacterLevel = requiredCharacterLevel,
                RequiredSkillId = requiredSkillId,
                RequiredSkillLevel = requiredSkillLevel
            };

            for (int i = 0; i < requiredSkills.Count; i++)
                displayData.Requirements.Add(requiredSkills[i]);

            string requiredGuildLevelFormula = GetStringValue(skillEntry["common"], "reqGuildLevel");
            if (!string.IsNullOrWhiteSpace(requiredGuildLevelFormula))
            {
                PopulateEvaluatedGuildValues(displayData.RequiredGuildLevels, requiredGuildLevelFormula, displayData.MaxLevel);
            }

            displayData.GuildPriceUnit = ResolveGuildPriceUnit(skillEntry["common"]);
            PopulateEvaluatedGuildValues(displayData.GuildActivationCosts, GetStringValue(skillEntry["common"], "price"), displayData.MaxLevel, displayData.GuildPriceUnit);
            PopulateEvaluatedGuildValues(displayData.GuildRenewalCosts, GetStringValue(skillEntry["common"], "extendPrice"), displayData.MaxLevel, displayData.GuildPriceUnit);
            PopulateEvaluatedGuildValues(displayData.GuildDurationsMinutes, GetStringValue(skillEntry["common"], "period"), displayData.MaxLevel);

            if (levelDescriptions != null)
            {
                foreach (var entry in levelDescriptions)
                    displayData.LevelDescriptions[entry.Key] = entry.Value;
            }

            if (formattedLevelDescriptions != null)
            {
                foreach (var entry in formattedLevelDescriptions)
                    displayData.FormattedLevelDescriptions[entry.Key] = entry.Value;
            }

            return displayData;
        }

        private static int ResolveGuildPriceUnit(WzImageProperty commonNode)
        {
            if (TryGetNumericPropertyValue(commonNode, "priceUnit", 1, out int priceUnit))
                return Math.Max(1, priceUnit);

            string unitString = GetStringValue(commonNode, "priceUnit");
            return int.TryParse(unitString, NumberStyles.Integer, CultureInfo.InvariantCulture, out priceUnit)
                ? Math.Max(1, priceUnit)
                : 1;
        }

        private static void PopulateEvaluatedGuildValues(
            IDictionary<int, int> target,
            string formula,
            int maxLevel,
            int multiplier = 1)
        {
            if (target == null || string.IsNullOrWhiteSpace(formula) || maxLevel <= 0)
                return;

            int resolvedMultiplier = Math.Max(1, multiplier);
            for (int level = 1; level <= maxLevel; level++)
            {
                if (!TryEvaluateFormula(formula, level, out int value))
                    continue;

                long scaled = (long)value * resolvedMultiplier;
                target[level] = scaled > int.MaxValue
                    ? int.MaxValue
                    : Math.Max(0, (int)scaled);
            }
        }

        private static int ResolveRequiredCharacterLevel(WzSubProperty skillEntry)
        {
            if (skillEntry == null)
                return 0;

            if (TryGetNumericPropertyValue(skillEntry["common"], "reqLevel", 1, out int requiredLevel) ||
                TryGetNumericPropertyValue(skillEntry["level"]?["1"], "reqLevel", 1, out requiredLevel) ||
                TryGetNumericPropertyValue(skillEntry["info"], "reqLevel", 1, out requiredLevel))
            {
                return Math.Max(0, requiredLevel);
            }

            return 0;
        }

        private static void ResolveRequiredSkill(
            IReadOnlyList<SkillRequirementDisplayData> requirements,
            out int requiredSkillId,
            out int requiredSkillLevel)
        {
            requiredSkillId = 0;
            requiredSkillLevel = 0;
            if (requirements == null || requirements.Count == 0)
                return;

            requiredSkillId = requirements[0].SkillId;
            requiredSkillLevel = Math.Max(0, requirements[0].RequiredLevel);
        }

        private static List<SkillRequirementDisplayData> BuildRequiredSkills(
            WzSubProperty skillEntry,
            WzImage stringImage,
            GraphicsDevice device)
        {
            var requirements = new List<SkillRequirementDisplayData>();
            if (skillEntry?["req"] is not WzSubProperty requirementNode)
                return requirements;

            foreach (WzImageProperty requirementEntry in requirementNode.WzProperties)
            {
                if (!int.TryParse(requirementEntry?.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int skillId) ||
                    !TryGetNumericValue(requirementEntry, out int skillLevel) ||
                    skillLevel <= 0)
                {
                    continue;
                }

                requirements.Add(new SkillRequirementDisplayData
                {
                    SkillId = skillId,
                    SkillName = ResolveSkillName(skillId, stringImage),
                    RequiredLevel = skillLevel,
                    IconTexture = LoadSkillIcon(skillId, device, out Point iconOrigin),
                    IconOrigin = iconOrigin
                });
            }

            return requirements;
        }

        private static string ResolveSkillName(int skillId, WzImage stringImage)
        {
            if (stringImage?[skillId.ToString(CultureInfo.InvariantCulture)] is WzSubProperty stringEntry &&
                stringEntry["name"] is WzStringProperty nameProp &&
                !string.IsNullOrWhiteSpace(nameProp.Value))
            {
                return nameProp.Value;
            }

            return $"Skill {skillId}";
        }

        private static Texture2D LoadSkillIcon(int skillId, GraphicsDevice device, out Point iconOrigin)
        {
            iconOrigin = Point.Zero;
            if (skillId <= 0 || device == null)
                return null;

            string skillImageName = GetSkillImageName(skillId / 10000);
            if (string.IsNullOrWhiteSpace(skillImageName))
                return null;

            WzImage skillImage = Program.FindImage("Skill", skillImageName);
            if (skillImage?["skill"]?[skillId.ToString(CultureInfo.InvariantCulture)]?["icon"] is not WzCanvasProperty iconProp)
                return null;

            iconOrigin = ResolveCanvasOrigin(iconProp);
            System.Drawing.Bitmap iconBitmap = iconProp.GetLinkedWzCanvasBitmap();
            return iconBitmap?.ToTexture2DAndDispose(device);
        }

        private static Point ResolveCanvasOrigin(WzCanvasProperty canvasProperty)
        {
            if (canvasProperty?[WzCanvasProperty.OriginPropertyName] is WzVectorProperty originProperty)
                return new Point(originProperty.X?.Value ?? 0, originProperty.Y?.Value ?? 0);

            return Point.Zero;
        }

        private static Dictionary<int, string> BuildLevelDescriptions(
            WzSubProperty skillEntry,
            WzSubProperty stringEntry,
            int maxLevel,
            bool preserveFormatting)
        {
            if (skillEntry == null || stringEntry == null || maxLevel <= 0)
                return null;

            var result = new Dictionary<int, string>();
            string sharedTemplate = GetStringValue(stringEntry, "h");
            if (string.IsNullOrWhiteSpace(sharedTemplate))
                sharedTemplate = GetStringValue(stringEntry, "ph");

            for (int level = 1; level <= maxLevel; level++)
            {
                WzImageProperty levelNode = skillEntry["level"]?[level.ToString(CultureInfo.InvariantCulture)];
                string explicitLevelText = GetStringValue(stringEntry, $"h{level}");
                if (string.IsNullOrWhiteSpace(explicitLevelText))
                {
                    string authoredLevelAlias = GetStringValue(levelNode, "hs");
                    if (!string.IsNullOrWhiteSpace(authoredLevelAlias))
                        explicitLevelText = GetStringValue(stringEntry, authoredLevelAlias);
                }

                string resolved = explicitLevelText;

                if (string.IsNullOrWhiteSpace(resolved) && !string.IsNullOrWhiteSpace(sharedTemplate))
                    resolved = ResolveSkillTemplate(sharedTemplate, skillEntry, level, maxLevel);

                string fallbackStats = BuildFallbackLevelDescription(skillEntry, level);
                if (string.IsNullOrWhiteSpace(resolved) || ContainsUnresolvedSkillToken(resolved))
                    resolved = fallbackStats;

                resolved = NormalizeSkillText(resolved, preserveFormatting);
                if (!string.IsNullOrWhiteSpace(resolved))
                    result[level] = resolved;
            }

            return result.Count > 0 ? result : null;
        }

        private static string ResolveSkillTemplate(string template, WzSubProperty skillEntry, int level, int maxLevel)
        {
            if (string.IsNullOrWhiteSpace(template) || skillEntry == null)
                return string.Empty;

            return Regex.Replace(template, "#([A-Za-z0-9_]+)", match =>
            {
                string token = match.Groups[1].Value;
                return TryResolveSkillToken(skillEntry, token, level, maxLevel, out string value)
                    ? value
                    : match.Value;
            });
        }

        private static bool TryResolveSkillToken(WzSubProperty skillEntry, string token, int level, int maxLevel, out string value)
        {
            value = null;
            if (skillEntry == null || string.IsNullOrWhiteSpace(token))
                return false;

            if (string.Equals(token, "level", StringComparison.OrdinalIgnoreCase))
            {
                value = level.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            if (string.Equals(token, "maxLevel", StringComparison.OrdinalIgnoreCase))
            {
                value = maxLevel.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            WzImageProperty levelNode = skillEntry["level"]?[level.ToString(CultureInfo.InvariantCulture)];
            if (TryGetNumericPropertyValue(levelNode, token, level, out int numericValue) ||
                TryGetNumericPropertyValue(skillEntry["common"], token, level, out numericValue) ||
                TryGetNumericPropertyValue(skillEntry["info"], token, level, out numericValue))
            {
                value = numericValue.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            string stringValue = GetStringValue(levelNode, token)
                                 ?? GetStringValue(skillEntry["common"], token)
                                 ?? GetStringValue(skillEntry["info"], token);
            if (!string.IsNullOrWhiteSpace(stringValue))
            {
                value = NormalizeSkillText(stringValue);
                return true;
            }

            return false;
        }

        private static string BuildFallbackLevelDescription(WzSubProperty skillEntry, int level)
        {
            if (skillEntry == null || level <= 0)
                return string.Empty;

            WzImageProperty levelNode = skillEntry["level"]?[level.ToString(CultureInfo.InvariantCulture)];
            WzImageProperty commonNode = skillEntry["common"];
            WzImageProperty infoNode = skillEntry["info"];
            var builder = new StringBuilder();
            var appendedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AppendFallbackStatsFromNode(builder, skillEntry, levelNode, commonNode, infoNode, level, levelNode, appendedKeys);
            AppendFallbackStatsFromNode(builder, skillEntry, levelNode, commonNode, infoNode, level, commonNode, appendedKeys);
            AppendFallbackStatsFromNode(builder, skillEntry, levelNode, commonNode, infoNode, level, infoNode, appendedKeys);
            AppendFallbackStatsInDefaultOrder(builder, skillEntry, levelNode, commonNode, infoNode, level, appendedKeys);

            return builder.ToString().Trim();
        }

        internal static string BuildFallbackLevelDescriptionForTests(WzSubProperty skillEntry, int level)
        {
            return BuildFallbackLevelDescription(skillEntry, level);
        }

        private static void AppendFallbackStatsFromNode(
            StringBuilder builder,
            WzSubProperty skillEntry,
            WzImageProperty levelNode,
            WzImageProperty commonNode,
            WzImageProperty infoNode,
            int level,
            WzImageProperty sourceNode,
            ISet<string> appendedKeys)
        {
            if (sourceNode == null || appendedKeys == null)
                return;

            foreach (WzImageProperty property in sourceNode.WzProperties)
            {
                if (property == null)
                    continue;

                string propertyName = property.Name;
                if (string.IsNullOrWhiteSpace(propertyName))
                    continue;

                string statKey = ResolveFallbackStatKey(propertyName);
                bool useGenericFallback = false;
                if (string.IsNullOrWhiteSpace(statKey))
                {
                    if (ReferenceEquals(sourceNode, infoNode) || !ShouldUseGenericFallbackStat(propertyName))
                        continue;

                    statKey = propertyName;
                    useGenericFallback = true;
                }

                if (appendedKeys.Contains(statKey))
                    continue;

                if (!TryAppendFallbackStat(builder, skillEntry, levelNode, commonNode, infoNode, level, statKey, useGenericFallback))
                    continue;

                appendedKeys.Add(statKey);
            }
        }

        private static void AppendFallbackStatsInDefaultOrder(
            StringBuilder builder,
            WzSubProperty skillEntry,
            WzImageProperty levelNode,
            WzImageProperty commonNode,
            WzImageProperty infoNode,
            int level,
            ISet<string> appendedKeys)
        {
            foreach (string statKey in FallbackSkillStatOrder)
            {
                if (appendedKeys.Contains(statKey))
                    continue;

                if (!TryAppendFallbackStat(builder, skillEntry, levelNode, commonNode, infoNode, level, statKey))
                    continue;

                appendedKeys.Add(statKey);
            }
        }

        private static string ResolveFallbackStatKey(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return null;

            if (string.Equals(propertyName, "ignoreTargetPDP", StringComparison.OrdinalIgnoreCase))
                return "ignoreMobpdpR";

            if (string.Equals(propertyName, "itemCon", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propertyName, "itemConNo", StringComparison.OrdinalIgnoreCase))
            {
                return "itemCon";
            }

            if (string.Equals(propertyName, "itemConsume", StringComparison.OrdinalIgnoreCase))
                return "itemConsume";

            if (string.Equals(propertyName, "lt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propertyName, "rb", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propertyName, "range", StringComparison.OrdinalIgnoreCase))
            {
                return "range";
            }

            return FallbackSkillStatDefinitions.ContainsKey(propertyName)
                ? propertyName
                : null;
        }

        private static bool TryAppendFallbackStat(
            StringBuilder builder,
            WzSubProperty skillEntry,
            WzImageProperty levelNode,
            WzImageProperty commonNode,
            WzImageProperty infoNode,
            int level,
            string statKey,
            bool allowGenericFallback = true)
        {
            if (string.Equals(statKey, "itemCon", StringComparison.OrdinalIgnoreCase))
            {
                int originalLength = builder.Length;
                AppendItemConsumptionStat(builder, skillEntry, levelNode, commonNode, infoNode, level, "itemCon", "itemConNo", "Consumes");
                return builder.Length != originalLength;
            }

            if (string.Equals(statKey, "itemConsume", StringComparison.OrdinalIgnoreCase))
            {
                int originalLength = builder.Length;
                AppendItemConsumptionStat(builder, skillEntry, levelNode, commonNode, infoNode, level, "itemConsume", null, "Consumes");
                return builder.Length != originalLength;
            }

            if (string.Equals(statKey, "range", StringComparison.OrdinalIgnoreCase))
            {
                int originalLength = builder.Length;
                AppendRangeStat(builder, levelNode, commonNode, infoNode, level);
                return builder.Length != originalLength;
            }

            if (string.Equals(statKey, "elemAttr", StringComparison.OrdinalIgnoreCase))
            {
                int originalLength = builder.Length;
                AppendElementAttributeStat(builder, levelNode, commonNode, infoNode);
                return builder.Length != originalLength;
            }

            if (string.Equals(statKey, "dotType", StringComparison.OrdinalIgnoreCase))
            {
                int originalLength = builder.Length;
                AppendDamageOverTimeTypeStat(builder, levelNode, commonNode, infoNode);
                return builder.Length != originalLength;
            }

            if (TryAppendContextualSingleLetterFallbackStat(
                    builder,
                    skillEntry,
                    levelNode,
                    commonNode,
                    infoNode,
                    level,
                    statKey))
            {
                return true;
            }

            if (!FallbackSkillStatDefinitions.TryGetValue(statKey, out FallbackSkillStatDefinition definition))
                return allowGenericFallback &&
                       TryAppendGenericFallbackStat(builder, skillEntry, levelNode, commonNode, infoNode, level, statKey);

            int originalBuilderLength = builder.Length;
            if (definition.IsPercent)
            {
                AppendPercentStat(builder, definition.Label, skillEntry, levelNode, commonNode, infoNode, level, definition.PropertyName);
            }
            else
            {
                AppendIntStat(
                    builder,
                    definition.Label,
                    skillEntry,
                    levelNode,
                    commonNode,
                    infoNode,
                    level,
                    definition.PropertyName,
                    definition.Formatter);
            }

            return builder.Length != originalBuilderLength;
        }

        private static bool TryAppendContextualSingleLetterFallbackStat(
            StringBuilder builder,
            WzSubProperty skillEntry,
            WzImageProperty levelNode,
            WzImageProperty commonNode,
            WzImageProperty infoNode,
            int level,
            string statKey)
        {
            string label = statKey switch
            {
                "x" => "Weapon ATT",
                "y" => "Magic ATT",
                "z" => "Weapon DEF",
                "u" => "Magic DEF",
                "v" => "Accuracy",
                "w" => "Avoidability",
                _ => null
            };

            if (label == null || !HasAdvancedBlessingFallbackShape(levelNode, commonNode, infoNode))
                return false;

            int originalLength = builder.Length;
            AppendIntStat(
                builder,
                label,
                skillEntry,
                levelNode,
                commonNode,
                infoNode,
                level,
                statKey,
                FormatSignedValue);
            return builder.Length != originalLength;
        }

        private static bool HasAdvancedBlessingFallbackShape(
            WzImageProperty levelNode,
            WzImageProperty commonNode,
            WzImageProperty infoNode)
        {
            return HasFallbackProperty(levelNode, commonNode, infoNode, "mpConReduce") &&
                   HasFallbackProperty(levelNode, commonNode, infoNode, "indieMhp") &&
                   HasFallbackProperty(levelNode, commonNode, infoNode, "indieMmp");
        }

        private static bool HasFallbackProperty(
            WzImageProperty levelNode,
            WzImageProperty commonNode,
            WzImageProperty infoNode,
            string propertyName)
        {
            return levelNode?[propertyName] != null ||
                   commonNode?[propertyName] != null ||
                   infoNode?[propertyName] != null;
        }

        private static bool TryAppendGenericFallbackStat(
            StringBuilder builder,
            WzSubProperty skillEntry,
            WzImageProperty levelNode,
            WzImageProperty commonNode,
            WzImageProperty infoNode,
            int level,
            string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName) ||
                !TryResolveGenericFallbackStatValue(skillEntry, levelNode, commonNode, infoNode, level, propertyName, out string value))
            {
                return false;
            }

            AppendStatLine(builder, FormatGenericFallbackStatLabel(propertyName), value);
            return true;
        }

        private static void AppendItemConsumptionStat(
            StringBuilder builder,
            WzSubProperty skillEntry,
            WzImageProperty levelNode,
            WzImageProperty commonNode,
            WzImageProperty infoNode,
            int level,
            string itemIdPropertyName,
            string quantityPropertyName,
            string label)
        {
            if (!TryGetLevelNumericValue(skillEntry, levelNode, commonNode, infoNode, itemIdPropertyName, level, out int itemId)
                || itemId <= 0)
            {
                return;
            }

            int quantity = 1;
            if (!string.IsNullOrWhiteSpace(quantityPropertyName)
                && TryGetLevelNumericValue(skillEntry, levelNode, commonNode, infoNode, quantityPropertyName, level, out int resolvedQuantity)
                && resolvedQuantity > 0)
            {
                quantity = resolvedQuantity;
            }

            string value = FormatFallbackItemConsumptionValue(itemId, quantity);
            AppendStatLine(builder, label, value);
        }

        private static void AppendPercentStat(
            StringBuilder builder,
            string label,
            WzSubProperty skillEntry,
            WzImageProperty levelNode,
            WzImageProperty commonNode,
            WzImageProperty infoNode,
            int level,
            string propertyName)
        {
            AppendIntStat(builder, label, skillEntry, levelNode, commonNode, infoNode, level, propertyName, value => $"{value}%");
        }

        private static void AppendIntStat(
            StringBuilder builder,
            string label,
            WzSubProperty skillEntry,
            WzImageProperty levelNode,
            WzImageProperty commonNode,
            WzImageProperty infoNode,
            int level,
            string propertyName,
            Func<int, string> formatter = null)
        {
            if (!TryGetLevelNumericValue(skillEntry, levelNode, commonNode, infoNode, propertyName, level, out int value))
                return;

            if (value == 0)
                return;

            AppendStatLine(builder, label, formatter != null ? formatter(value) : value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendRangeStat(
            StringBuilder builder,
            WzImageProperty levelNode,
            WzImageProperty commonNode,
            WzImageProperty infoNode,
            int level)
        {
            int left = 0;
            int right = 0;
            int vertical = 0;
            int range = 0;
            bool hasBounds = false;

            if (TryGetVectorValue(levelNode, commonNode, infoNode, "lt", out int ltX, out int ltY))
            {
                left = Math.Abs(ltX);
                vertical = Math.Max(vertical, Math.Abs(ltY));
                hasBounds = true;
            }

            if (TryGetVectorValue(levelNode, commonNode, infoNode, "rb", out int rbX, out int rbY))
            {
                right = Math.Abs(rbX);
                vertical = Math.Max(vertical, Math.Abs(rbY));
                hasBounds = true;
            }

            if (!hasBounds && !TryGetLevelNumericValue(null, levelNode, commonNode, infoNode, "range", level, out range))
                return;

            if (!hasBounds)
            {
                left = range;
                right = range;
            }

            string rangeText = vertical > 0
                ? $"{left} / {right} / {vertical}"
                : $"{left} / {right}";
            AppendStatLine(builder, "Range", rangeText);
        }

        private static void AppendElementAttributeStat(
            StringBuilder builder,
            WzImageProperty levelNode,
            WzImageProperty commonNode,
            WzImageProperty infoNode)
        {
            string elementAttribute = GetStringValue(levelNode, "elemAttr")
                                      ?? GetStringValue(commonNode, "elemAttr")
                                      ?? GetStringValue(infoNode, "elemAttr");
            if (string.IsNullOrWhiteSpace(elementAttribute))
                return;

            string formatted = FormatElementAttributeValue(elementAttribute);
            if (!string.IsNullOrWhiteSpace(formatted))
                AppendStatLine(builder, "Element", formatted);
        }

        private static void AppendDamageOverTimeTypeStat(
            StringBuilder builder,
            WzImageProperty levelNode,
            WzImageProperty commonNode,
            WzImageProperty infoNode)
        {
            string dotType = GetStringValue(levelNode, "dotType")
                             ?? GetStringValue(commonNode, "dotType")
                             ?? GetStringValue(infoNode, "dotType");
            if (string.IsNullOrWhiteSpace(dotType))
                return;

            string formatted = FormatDamageOverTimeTypeValue(dotType);
            if (!string.IsNullOrWhiteSpace(formatted))
                AppendStatLine(builder, "Damage Over Time Type", formatted);
        }

        private static bool TryGetVectorValue(
            WzImageProperty levelNode,
            WzImageProperty commonNode,
            WzImageProperty infoNode,
            string propertyName,
            out int x,
            out int y)
        {
            x = 0;
            y = 0;

            if (TryReadVector(levelNode?[propertyName], out x, out y))
                return true;
            if (TryReadVector(commonNode?[propertyName], out x, out y))
                return true;
            return TryReadVector(infoNode?[propertyName], out x, out y);
        }

        private static bool TryReadVector(WzImageProperty property, out int x, out int y)
        {
            x = 0;
            y = 0;

            if (property is not WzVectorProperty vector)
                return false;

            x = vector.X?.Value ?? 0;
            y = vector.Y?.Value ?? 0;
            return true;
        }

        private static bool TryGetLevelNumericValue(
            WzSubProperty skillEntry,
            WzImageProperty levelNode,
            WzImageProperty commonNode,
            WzImageProperty infoNode,
            string propertyName,
            int level,
            out int value)
        {
            if (TryGetNumericPropertyValue(levelNode, propertyName, level, out value) ||
                TryGetNumericPropertyValue(commonNode, propertyName, level, out value) ||
                TryGetNumericPropertyValue(infoNode, propertyName, level, out value))
            {
                return true;
            }

            if (skillEntry != null)
                return TryResolveSkillToken(skillEntry, propertyName, level, Math.Max(level, 1), out string resolved)
                    && int.TryParse(resolved, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

            value = 0;
            return false;
        }

        private static bool TryResolveGenericFallbackStatValue(
            WzSubProperty skillEntry,
            WzImageProperty levelNode,
            WzImageProperty commonNode,
            WzImageProperty infoNode,
            int level,
            string propertyName,
            out string value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(propertyName))
                return false;

            if (skillEntry != null &&
                TryResolveSkillToken(skillEntry, propertyName, level, Math.Max(level, 1), out string resolvedTokenValue) &&
                !string.IsNullOrWhiteSpace(resolvedTokenValue) &&
                !string.Equals(resolvedTokenValue, "0", StringComparison.Ordinal))
            {
                value = resolvedTokenValue;
                return true;
            }

            string rawValue = GetStringValue(levelNode, propertyName)
                              ?? GetStringValue(commonNode, propertyName)
                              ?? GetStringValue(infoNode, propertyName);
            if (string.IsNullOrWhiteSpace(rawValue))
                return false;

            string normalized = NormalizeSkillText(rawValue);
            if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, "0", StringComparison.Ordinal))
                return false;

            value = normalized;
            return true;
        }

        private static bool ShouldUseGenericFallbackStat(string propertyName)
        {
            return !string.IsNullOrWhiteSpace(propertyName) &&
                   !FallbackSkillHiddenProperties.Contains(propertyName);
        }

        private static string FormatGenericFallbackStatLabel(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return string.Empty;

            if (propertyName.Length == 1)
                return propertyName.ToUpperInvariant();

            string withWordBoundaries = Regex.Replace(propertyName, "([a-z0-9])([A-Z])", "$1 $2");
            string withSeparatedSuffixes = Regex.Replace(withWordBoundaries, "([A-Za-z])([0-9])", "$1 $2");
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(withSeparatedSuffixes.Replace('_', ' '));
        }

        private static bool ContainsUnresolvedSkillToken(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string normalized = text.Replace("##", string.Empty);
            normalized = Regex.Replace(normalized, "#[A-Za-z][^#]*#", string.Empty);
            return Regex.IsMatch(normalized, "#[A-Za-z0-9_]+");
        }

        private static void AppendStatLine(StringBuilder builder, string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append(label);
            builder.Append(": ");
            builder.Append(value);
        }

        private static string FormatSignedValue(int value)
        {
            return value > 0
                ? $"+{value.ToString(CultureInfo.InvariantCulture)}"
                : value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatEnabledValue(int value)
        {
            return value > 0 ? "Enabled" : "Disabled";
        }

        private static string FormatNegativePercentValue(int value)
        {
            return $"-{Math.Abs(value).ToString(CultureInfo.InvariantCulture)}%";
        }

        private static string FormatActionSpeedValue(int value)
        {
            if (value == 0)
                return string.Empty;

            int levelDelta = Math.Abs(value);
            string levelText = levelDelta == 1 ? "level" : "levels";
            return $"{levelDelta.ToString(CultureInfo.InvariantCulture)} {levelText}";
        }

        internal static string FormatFallbackItemConsumptionValue(int itemId, int quantity, string resolvedItemName = null)
        {
            if (itemId <= 0)
                return string.Empty;

            string itemName = resolvedItemName != null
                ? resolvedItemName.Trim()
                : TryResolveFallbackItemName(itemId);
            if (string.IsNullOrWhiteSpace(itemName))
                itemName = $"Item {itemId.ToString(CultureInfo.InvariantCulture)}";

            int count = Math.Max(1, quantity);
            return count > 1
                ? $"{itemName} x{count.ToString(CultureInfo.InvariantCulture)}"
                : itemName;
        }

        internal static bool TryResolveFallbackStatPresentation(string statKey, int value, out string label, out string formattedValue)
        {
            label = null;
            formattedValue = null;

            if (string.IsNullOrWhiteSpace(statKey)
                || value == 0
                || !FallbackSkillStatDefinitions.TryGetValue(statKey, out FallbackSkillStatDefinition definition))
            {
                return false;
            }

            label = definition.Label;
            formattedValue = definition.IsPercent
                ? $"{value.ToString(CultureInfo.InvariantCulture)}%"
                : definition.Formatter != null
                    ? definition.Formatter(value)
                    : value.ToString(CultureInfo.InvariantCulture);
            return !string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(formattedValue);
        }

        private static string TryResolveFallbackItemName(int itemId)
        {
            return InventoryItemMetadataResolver.TryResolveItemName(itemId, out string resolvedName)
                && !string.IsNullOrWhiteSpace(resolvedName)
                ? resolvedName.Trim()
                : string.Empty;
        }

        private static string FormatElementAttributeValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var labels = new List<string>();
            foreach (char attribute in value.Trim())
            {
                string label = attribute switch
                {
                    'f' or 'F' => "Fire",
                    'i' or 'I' => "Ice",
                    'l' or 'L' => "Lightning",
                    's' or 'S' => "Poison",
                    'h' or 'H' => "Holy",
                    'd' or 'D' => "Dark",
                    'p' or 'P' => "Physical",
                    _ => null
                };

                if (!string.IsNullOrWhiteSpace(label) && !labels.Contains(label, StringComparer.OrdinalIgnoreCase))
                    labels.Add(label);
            }

            return labels.Count > 0
                ? string.Join(", ", labels)
                : value.Trim();
        }

        private static string FormatDamageOverTimeTypeValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim().ToLowerInvariant() switch
            {
                "burn" => "Burn",
                "frostbite" => "Frostbite",
                "invenom" => "Venom",
                "aura" => "Aura",
                _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Trim().ToLowerInvariant())
            };
        }

        private sealed class FallbackSkillStatDefinition
        {
            public FallbackSkillStatDefinition(
                string propertyName,
                string label,
                bool isPercent = false,
                Func<int, string> formatter = null)
            {
                PropertyName = propertyName;
                Label = label;
                IsPercent = isPercent;
                Formatter = formatter;
            }

            public string PropertyName { get; }
            public string Label { get; }
            public bool IsPercent { get; }
            public Func<int, string> Formatter { get; }
        }

        private static readonly string[] FallbackSkillStatOrder =
        {
            "damage",
            "fixdamage",
            "dot",
            "attackCount",
            "mobCount",
            "mpCon",
            "hpCon",
            "moneyCon",
            "damagebymoneyCon",
            "iceGageCon",
            "massSpell",
            "magicSteal",
            "itemCon",
            "itemConsume",
            "bulletConsume",
            "cooltime",
            "time",
            "dotTime",
            "dotInterval",
            "dotType",
            "subProp",
            "subTime",
            "reqGuildLevel",
            "range",
            "mastery",
            "cr",
            "criticaldamageMin",
            "criticaldamageMax",
            "prop",
            "ar",
            "er",
            "damR",
            "mhpR",
            "mmpR",
            "pddR",
            "mddR",
            "accR",
            "evaR",
            "asrR",
            "terR",
            "ignoreMobpdpR",
            "bdR",
            "expR",
            "dropR",
            "mesoR",
            "epad",
            "emad",
            "epdd",
            "emdd",
            "emhp",
            "emmp",
            "str",
            "dex",
            "int",
            "luk",
            "pad",
            "mad",
            "pdd",
            "mdd",
            "acc",
            "eva",
            "speed",
            "jump",
            "speedMax",
            "actionSpeed",
            "indieAllStat",
            "indiePad",
            "indieMad",
            "indieAcc",
            "indieEva",
            "indieDamR",
            "indieMhp",
            "indieMmp",
            "indieMhpR",
            "indieMmpR",
            "indieSpeed",
            "indieJump",
            "hp",
            "mp",
            "mpConReduce",
            "padX",
            "madX",
            "bulletCount",
            "bulletSpeed",
            "morph",
            "selfDestruction",
            "elemAttr",
            "ignoreMobDamR",
            "psdSpeed",
            "psdJump",
            "x",
            "y",
            "z"
        };

        private static readonly IReadOnlyDictionary<string, FallbackSkillStatDefinition> FallbackSkillStatDefinitions =
            new Dictionary<string, FallbackSkillStatDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["damage"] = new("damage", "Damage", isPercent: true),
                ["fixdamage"] = new("fixdamage", "Fixed Damage"),
                ["dot"] = new("dot", "Damage Over Time"),
                ["attackCount"] = new("attackCount", "Attack Count"),
                ["mobCount"] = new("mobCount", "Mob Count"),
                ["mpCon"] = new("mpCon", "MP Cost"),
                ["hpCon"] = new("hpCon", "HP Cost"),
                ["moneyCon"] = new("moneyCon", "Meso Cost"),
                ["damagebymoneyCon"] = new("damagebymoneyCon", "Damage by Meso Cost", formatter: FormatEnabledValue),
                ["iceGageCon"] = new("iceGageCon", "Ice Gauge Cost"),
                ["massSpell"] = new("massSpell", "Mass Spell", formatter: FormatEnabledValue),
                ["magicSteal"] = new("magicSteal", "Magic Steal", formatter: FormatEnabledValue),
                ["bulletConsume"] = new("bulletConsume", "Ammo Cost"),
                ["cooltime"] = new("cooltime", "Cooldown", formatter: value => $"{value} sec"),
                ["time"] = new("time", "Duration", formatter: value => $"{value} sec"),
                ["dotTime"] = new("dotTime", "Damage Over Time Duration", formatter: value => $"{value} sec"),
                ["dotInterval"] = new("dotInterval", "Damage Over Time Interval", formatter: value => $"{value} sec"),
                ["subProp"] = new("subProp", "Secondary Effect Chance", isPercent: true),
                ["subTime"] = new("subTime", "Secondary Effect Duration", formatter: value => $"{value} sec"),
                ["reqGuildLevel"] = new("reqGuildLevel", "Required Guild Level"),
                ["mastery"] = new("mastery", "Mastery", isPercent: true),
                ["cr"] = new("cr", "Critical Rate", isPercent: true),
                ["criticaldamageMin"] = new("criticaldamageMin", "Critical Damage (Min)", isPercent: true),
                ["criticaldamageMax"] = new("criticaldamageMax", "Critical Damage (Max)", isPercent: true),
                ["prop"] = new("prop", "Chance", isPercent: true),
                ["ar"] = new("ar", "Accuracy", isPercent: true),
                ["er"] = new("er", "Avoidability", isPercent: true),
                ["damR"] = new("damR", "Damage Reduction", isPercent: true),
                ["mhpR"] = new("mhpR", "Max HP", isPercent: true),
                ["mmpR"] = new("mmpR", "Max MP", isPercent: true),
                ["pddR"] = new("pddR", "DEF", isPercent: true),
                ["mddR"] = new("mddR", "Magic DEF", isPercent: true),
                ["accR"] = new("accR", "Accuracy", isPercent: true),
                ["evaR"] = new("evaR", "Avoidability", isPercent: true),
                ["asrR"] = new("asrR", "Abnormal Status Resistance", isPercent: true),
                ["terR"] = new("terR", "Elemental Resistance", isPercent: true),
                ["ignoreMobpdpR"] = new("ignoreMobpdpR", "Ignore Enemy DEF", isPercent: true),
                ["bdR"] = new("bdR", "Boss Damage", isPercent: true),
                ["expR"] = new("expR", "Bonus EXP", isPercent: true),
                ["dropR"] = new("dropR", "Drop Rate", isPercent: true),
                ["mesoR"] = new("mesoR", "Meso Rate", isPercent: true),
                ["epad"] = new("epad", "Weapon ATT", formatter: FormatSignedValue),
                ["emad"] = new("emad", "Magic ATT", formatter: FormatSignedValue),
                ["epdd"] = new("epdd", "Weapon DEF", formatter: FormatSignedValue),
                ["emdd"] = new("emdd", "Magic DEF", formatter: FormatSignedValue),
                ["emhp"] = new("emhp", "HP", formatter: FormatSignedValue),
                ["emmp"] = new("emmp", "MP", formatter: FormatSignedValue),
                ["str"] = new("str", "STR", formatter: FormatSignedValue),
                ["dex"] = new("dex", "DEX", formatter: FormatSignedValue),
                ["int"] = new("int", "INT", formatter: FormatSignedValue),
                ["luk"] = new("luk", "LUK", formatter: FormatSignedValue),
                ["pad"] = new("pad", "Weapon ATT", formatter: FormatSignedValue),
                ["mad"] = new("mad", "Magic ATT", formatter: FormatSignedValue),
                ["pdd"] = new("pdd", "Weapon DEF", formatter: FormatSignedValue),
                ["mdd"] = new("mdd", "Magic DEF", formatter: FormatSignedValue),
                ["acc"] = new("acc", "Accuracy", formatter: FormatSignedValue),
                ["eva"] = new("eva", "Avoidability", formatter: FormatSignedValue),
                ["speed"] = new("speed", "Speed", formatter: FormatSignedValue),
                ["jump"] = new("jump", "Jump", formatter: FormatSignedValue),
                ["speedMax"] = new("speedMax", "Max Movement Speed"),
                ["actionSpeed"] = new("actionSpeed", "Attack Speed", formatter: FormatActionSpeedValue),
                ["indieAllStat"] = new("indieAllStat", "All Stats", formatter: FormatSignedValue),
                ["indiePad"] = new("indiePad", "Weapon ATT", formatter: FormatSignedValue),
                ["indieMad"] = new("indieMad", "Magic ATT", formatter: FormatSignedValue),
                ["indieAcc"] = new("indieAcc", "Accuracy", formatter: FormatSignedValue),
                ["indieEva"] = new("indieEva", "Avoidability", formatter: FormatSignedValue),
                ["indieDamR"] = new("indieDamR", "Damage", isPercent: true),
                ["indieMhp"] = new("indieMhp", "Max HP", formatter: FormatSignedValue),
                ["indieMmp"] = new("indieMmp", "Max MP", formatter: FormatSignedValue),
                ["indieMhpR"] = new("indieMhpR", "Max HP", isPercent: true),
                ["indieMmpR"] = new("indieMmpR", "Max MP", isPercent: true),
                ["indieSpeed"] = new("indieSpeed", "Speed", formatter: FormatSignedValue),
                ["indieJump"] = new("indieJump", "Jump", formatter: FormatSignedValue),
                ["hp"] = new("hp", "HP Recovery"),
                ["mp"] = new("mp", "MP Recovery"),
                ["mpConReduce"] = new("mpConReduce", "MP Consumption", formatter: FormatNegativePercentValue),
                ["padX"] = new("padX", "Weapon ATT", formatter: FormatSignedValue),
                ["madX"] = new("madX", "Magic ATT", formatter: FormatSignedValue),
                ["bulletCount"] = new("bulletCount", "Bullet Count"),
                ["bulletSpeed"] = new("bulletSpeed", "Bullet Speed"),
                ["morph"] = new("morph", "Morph"),
                ["selfDestruction"] = new("selfDestruction", "Self-Destruction Damage", isPercent: true),
                ["ignoreMobDamR"] = new("ignoreMobDamR", "Ignore Enemy Damage Reduction", isPercent: true),
                ["psdSpeed"] = new("psdSpeed", "Movement Speed", formatter: FormatSignedValue),
                ["psdJump"] = new("psdJump", "Jump", formatter: FormatSignedValue),
                ["x"] = new("x", "X"),
                ["y"] = new("y", "Y"),
                ["z"] = new("z", "Z")
            };

        private static readonly ISet<string> FallbackSkillHiddenProperties =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "maxLevel",
                "masterLevel",
                "priceUnit",
                "invisible",
                "hs",
                "type"
            };

        private static bool TryGetNumericPropertyValue(WzImageProperty node, string name, int formulaX, out int value)
        {
            value = 0;
            if (node == null || string.IsNullOrWhiteSpace(name))
                return false;

            WzImageProperty child = node[name];
            switch (child)
            {
                case WzIntProperty intProp:
                    value = intProp.Value;
                    return true;
                case WzShortProperty shortProp:
                    value = shortProp.Value;
                    return true;
                case WzLongProperty longProp:
                    value = (int)longProp.Value;
                    return true;
                case WzFloatProperty floatProp:
                    value = (int)Math.Round(floatProp.Value, MidpointRounding.AwayFromZero);
                    return true;
                case WzDoubleProperty doubleProp:
                    value = (int)Math.Round(doubleProp.Value, MidpointRounding.AwayFromZero);
                    return true;
                case WzStringProperty stringProp:
                    return TryEvaluateFormula(stringProp.Value, formulaX, out value);
                default:
                    return false;
            }
        }

        private static bool TryGetNumericValue(WzImageProperty property, out int value)
        {
            value = 0;
            switch (property)
            {
                case WzIntProperty intProp:
                    value = intProp.Value;
                    return true;
                case WzShortProperty shortProp:
                    value = shortProp.Value;
                    return true;
                case WzLongProperty longProp:
                    value = (int)longProp.Value;
                    return true;
                case WzFloatProperty floatProp:
                    value = (int)Math.Round(floatProp.Value, MidpointRounding.AwayFromZero);
                    return true;
                case WzDoubleProperty doubleProp:
                    value = (int)Math.Round(doubleProp.Value, MidpointRounding.AwayFromZero);
                    return true;
                case WzStringProperty stringProp:
                    return TryEvaluateFormula(stringProp.Value, 1, out value);
                default:
                    return false;
            }
        }

        private static string GetStringValue(WzImageProperty node, string name)
        {
            if (node == null || string.IsNullOrWhiteSpace(name))
                return null;

            return node[name] is WzStringProperty stringProp
                ? stringProp.Value
                : null;
        }

        internal static string NormalizeSkillDescriptionForTooltip(
            string text,
            bool hasExplicitRequirements,
            bool preserveFormatting = false)
        {
            string normalized = NormalizeSkillLineBreaks(text);
            if (hasExplicitRequirements)
                normalized = RemoveEmbeddedRequirementLines(normalized);

            return FinalizeNormalizedSkillText(normalized, preserveFormatting);
        }

        private static string NormalizeSkillText(string text, bool preserveFormatting = false)
        {
            return FinalizeNormalizedSkillText(NormalizeSkillLineBreaks(text), preserveFormatting);
        }

        private static string NormalizeSkillLineBreaks(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string normalized = text
                .Replace("\\r\\n", "\n")
                .Replace("\\n", "\n")
                .Replace("\\r", "\n")
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Replace('\u00A0', ' ');

            normalized = Regex.Replace(
                normalized,
                @"\\+(?=\s*Required\s+Skill\s*:)",
                "\n",
                RegexOptions.IgnoreCase);

            return normalized;
        }

        private static string RemoveEmbeddedRequirementLines(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string[] lines = text.Split('\n');
            var keptLines = new List<string>(lines.Length);

            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                string trimmed = line.TrimStart();
                string trimmedWithoutSlash = trimmed.TrimStart('\\').TrimStart();
                int colonIndex = trimmedWithoutSlash.IndexOf(':');
                bool isRequirementHeader = colonIndex >= 0 &&
                                           trimmedWithoutSlash.StartsWith("Required Skill", StringComparison.OrdinalIgnoreCase);
                if (!isRequirementHeader)
                {
                    keptLines.Add(line);
                    continue;
                }

                string trailingValue = trimmedWithoutSlash[(colonIndex + 1)..].Trim();
                if (trailingValue.Length > 0)
                    continue;

                while (index + 1 < lines.Length && string.IsNullOrWhiteSpace(lines[index + 1]))
                    index++;

                if (index + 1 < lines.Length)
                    index++;
            }

            return string.Join("\n", keptLines);
        }

        private static string FinalizeNormalizedSkillText(string text, bool preserveFormatting)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string normalized = text;
            if (!preserveFormatting)
                normalized = Regex.Replace(normalized, "#([a-zA-Z])([^#]+)#", "$2");
            normalized = normalized.Replace("##", "#");
            normalized = Regex.Replace(normalized, "[ \t]+\n", "\n");
            normalized = Regex.Replace(normalized, "\n{3,}", "\n\n");
            normalized = Regex.Replace(normalized, "[ \t]{2,}", " ");

            return normalized.Trim();
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

        /// <summary>
        /// Get the skill image name for a job ID
        /// </summary>
        private static string GetSkillImageName(int jobId)
        {
            if (JobToSkillImage.TryGetValue(jobId, out string imageName))
                return imageName;

            // Try to construct image name from job ID
            return $"{jobId:D3}.img";
        }

        /// <summary>
        /// Get job advancement level (0-4) from job ID
        /// </summary>
        public static int GetJobAdvancementLevel(int jobId)
        {
            if (jobId == 0) return 0; // Beginner

            // GM/SuperGM books still live on the first job tab in the client UI.
            if (jobId >= 800 && jobId < 1000)
                return 1;

            int baseJob = jobId / 100;
            int advancement = jobId % 100;

            if (advancement == 0) return 1; // 1st job
            if (advancement < 10) return 1; // Still 1st job variations
            if (advancement < 20) return 2; // 2nd job
            if (advancement < 100)
            {
                int subAdv = advancement % 10;
                if (subAdv == 0) return 2;
                if (subAdv == 1) return 3;
                if (subAdv == 2) return 4;
            }

            return 1; // Default to 1st job
        }

        /// <summary>
        /// Load the job/skillbook icon for a specific job
        /// </summary>
        /// <param name="jobId">Job ID (0 = Beginner, 100 = Warrior, etc.)</param>
        /// <param name="device">Graphics device for texture creation</param>
        /// <returns>The job icon texture, or null if not found</returns>
        public static Texture2D LoadJobIcon(int jobId, GraphicsDevice device)
        {
            string skillImageName = GetSkillImageName(jobId);
            if (string.IsNullOrEmpty(skillImageName))
                return null;

            WzImage skillImage = Program.FindImage("Skill", skillImageName);
            if (skillImage == null)
                return null;

            // Get info/icon
            WzSubProperty infoProp = (WzSubProperty)skillImage["info"];
            if (infoProp == null)
                return null;

            WzCanvasProperty iconProp = (WzCanvasProperty)infoProp["icon"];
            if (iconProp == null)
                return null;

            System.Drawing.Bitmap iconBitmap = iconProp.GetLinkedWzCanvasBitmap();
            return iconBitmap?.ToTexture2DAndDispose(device);
        }

        /// <summary>
        /// Get job name from String.wz
        /// </summary>
        /// <param name="jobId">Job ID</param>
        /// <returns>Job name or default name</returns>
        public static string GetJobName(int jobId)
        {
            // Default job names
            var defaultNames = new Dictionary<int, string>
            {
                { 0, "Beginner" },
                { 100, "Warrior" }, { 110, "Fighter" }, { 111, "Crusader" }, { 112, "Hero" },
                { 120, "Page" }, { 121, "White Knight" }, { 122, "Paladin" },
                { 130, "Spearman" }, { 131, "Dragon Knight" }, { 132, "Dark Knight" },
                { 200, "Magician" }, { 210, "Wizard (F/P)" }, { 211, "Mage (F/P)" }, { 212, "Arch Mage (F/P)" },
                { 220, "Wizard (I/L)" }, { 221, "Mage (I/L)" }, { 222, "Arch Mage (I/L)" },
                { 230, "Cleric" }, { 231, "Priest" }, { 232, "Bishop" },
                { 300, "Archer" }, { 310, "Hunter" }, { 311, "Ranger" }, { 312, "Bowmaster" },
                { 320, "Crossbowman" }, { 321, "Sniper" }, { 322, "Marksman" },
                { 400, "Rogue" }, { 410, "Assassin" }, { 411, "Hermit" }, { 412, "Night Lord" },
                { 420, "Bandit" }, { 421, "Chief Bandit" }, { 422, "Shadower" },
                { 500, "Pirate" }, { 510, "Brawler" }, { 511, "Marauder" }, { 512, "Buccaneer" },
                { 520, "Gunslinger" }, { 521, "Outlaw" }, { 522, "Corsair" },
                { 900, "GM" },
                { 910, "SuperGM" },
            };

            if (defaultNames.TryGetValue(jobId, out string name))
                return name;

            return $"Job {jobId}";
        }

        private static bool TryBuildRecommendedSkillEntries(
            WzImageProperty property,
            ISet<int> validSkillIds,
            out List<RecommendedSkillEntry> entries)
        {
            entries = null;
            if (property?.WzProperties == null || property.WzProperties.Count == 0)
                return false;

            entries = new List<RecommendedSkillEntry>();
            foreach (WzImageProperty child in property.WzProperties)
            {
                if (child == null)
                    continue;

                if (!int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int spentSpThreshold) ||
                    spentSpThreshold < 0 ||
                    !TryGetIntValue(child, out int skillId) ||
                    skillId <= 0 ||
                    (validSkillIds != null && validSkillIds.Count > 0 && !validSkillIds.Contains(skillId)))
                {
                    continue;
                }

                entries.Add(new RecommendedSkillEntry(spentSpThreshold, skillId));
            }

            if (entries.Count == 0)
                return false;

            entries.Sort((left, right) =>
            {
                int thresholdCompare = left.SpentSpThreshold.CompareTo(right.SpentSpThreshold);
                return thresholdCompare != 0 ? thresholdCompare : left.SkillId.CompareTo(right.SkillId);
            });
            return true;
        }

        private static bool TryGetIntValue(WzObject node, out int value)
        {
            value = 0;

            switch (node)
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
                case WzFloatProperty floatProperty:
                    value = (int)Math.Round(floatProperty.Value, MidpointRounding.AwayFromZero);
                    return true;
            }

            if (node is WzImageProperty imageProperty && imageProperty.WzProperties != null && imageProperty.WzProperties.Count == 1)
            {
                return TryGetIntValue(imageProperty.WzProperties[0], out value);
            }

            return false;
        }

        private static bool ResolveBooleanProperty(WzImageProperty node, string name)
        {
            if (node == null || string.IsNullOrWhiteSpace(name))
                return false;

            return TryGetNumericPropertyValue(node, name, 1, out int value) && value != 0;
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
                        value += ParseTerm();
                    else if (Match('-'))
                        value -= ParseTerm();
                    else
                        return value;
                }
            }

            private double ParseTerm()
            {
                double value = ParseFactor();
                while (true)
                {
                    SkipWhitespace();
                    if (Match('*'))
                        value *= ParseFactor();
                    else if (Match('/'))
                        value /= ParseFactor();
                    else
                        return value;
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
                    throw new FormatException($"Expected number at position {_index} in '{_expression}'.");

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
                    throw new FormatException($"Expected '{ch}' in '{_expression}'.");
            }

            private void SkipWhitespace()
            {
                while (_index < _expression.Length && char.IsWhiteSpace(_expression[_index]))
                    _index++;
            }
        }
    }
}
