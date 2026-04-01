using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Util;
using HaCreator.MapSimulator.Character.Skills;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
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

            var validSkillIdSet = validSkillIds != null
                ? new HashSet<int>(validSkillIds)
                : new HashSet<int>();
            if (validSkillIdSet.Count == 0)
                return Array.Empty<RecommendedSkillEntry>();

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

            if (infoProperty != null)
            {
                WzIntProperty maxLevelProp = (WzIntProperty)infoProperty["maxLevel"];
                if (maxLevelProp != null)
                    maxLevel = maxLevelProp.Value;

                WzIntProperty invisibleProp = (WzIntProperty)infoProperty["invisible"];
                if (invisibleProp != null)
                    invisible = invisibleProp.Value == 1;
            }

            // Get level info for max level determination
            WzSubProperty levelProperty = (WzSubProperty)skillEntry["level"];
            if (levelProperty != null && maxLevel == 0)
            {
                maxLevel = levelProperty.WzProperties.Count;
            }

            // Skip invisible skills
            if (invisible)
                return null;

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

            formattedDescription = NormalizeSkillText(description, preserveFormatting: true);
            description = NormalizeSkillText(description);
            int requiredCharacterLevel = ResolveRequiredCharacterLevel(skillEntry);
            ResolveRequiredSkill(skillEntry, out int requiredSkillId, out int requiredSkillLevel);

            var displayData = new SkillDisplayData
            {
                SkillId = skillId,
                IconTexture = iconTexture,
                IconDisabledTexture = disabledIconTexture,
                IconMouseOverTexture = mouseOverIconTexture,
                SkillName = skillName,
                Description = description,
                FormattedDescription = formattedDescription,
                CurrentLevel = 0,
                MaxLevel = Math.Max(1, maxLevel),
                RequiredCharacterLevel = requiredCharacterLevel,
                RequiredSkillId = requiredSkillId,
                RequiredSkillLevel = requiredSkillLevel
            };

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

        private static void ResolveRequiredSkill(WzSubProperty skillEntry, out int requiredSkillId, out int requiredSkillLevel)
        {
            requiredSkillId = 0;
            requiredSkillLevel = 0;
            if (skillEntry?["req"] is not WzSubProperty requirementNode)
                return;

            foreach (WzImageProperty requirementEntry in requirementNode.WzProperties)
            {
                if (!int.TryParse(requirementEntry?.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int skillId))
                    continue;

                if (!TryGetNumericValue(requirementEntry, out int skillLevel) || skillLevel <= 0)
                    continue;

                requiredSkillId = skillId;
                requiredSkillLevel = skillLevel;
                return;
            }
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
                string explicitLevelText = GetStringValue(stringEntry, $"h{level}");
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

            AppendPercentStat(builder, "Damage", skillEntry, levelNode, commonNode, infoNode, level, "damage");
            AppendIntStat(builder, "Attack Count", skillEntry, levelNode, commonNode, infoNode, level, "attackCount");
            AppendIntStat(builder, "Mob Count", skillEntry, levelNode, commonNode, infoNode, level, "mobCount");
            AppendIntStat(builder, "MP Cost", skillEntry, levelNode, commonNode, infoNode, level, "mpCon");
            AppendIntStat(builder, "HP Cost", skillEntry, levelNode, commonNode, infoNode, level, "hpCon");
            AppendIntStat(builder, "Cooldown", skillEntry, levelNode, commonNode, infoNode, level, "cooltime", value => $"{value} sec");
            AppendIntStat(builder, "Duration", skillEntry, levelNode, commonNode, infoNode, level, "time", value => $"{value} sec");
            AppendRangeStat(builder, levelNode, commonNode, infoNode, level);
            AppendPercentStat(builder, "Mastery", skillEntry, levelNode, commonNode, infoNode, level, "mastery");
            AppendPercentStat(builder, "Critical Rate", skillEntry, levelNode, commonNode, infoNode, level, "cr");
            AppendPercentStat(builder, "Chance", skillEntry, levelNode, commonNode, infoNode, level, "prop");
            AppendIntStat(builder, "PAD", skillEntry, levelNode, commonNode, infoNode, level, "pad", FormatSignedValue);
            AppendIntStat(builder, "MAD", skillEntry, levelNode, commonNode, infoNode, level, "mad", FormatSignedValue);
            AppendIntStat(builder, "PDD", skillEntry, levelNode, commonNode, infoNode, level, "pdd", FormatSignedValue);
            AppendIntStat(builder, "MDD", skillEntry, levelNode, commonNode, infoNode, level, "mdd", FormatSignedValue);
            AppendIntStat(builder, "ACC", skillEntry, levelNode, commonNode, infoNode, level, "acc", FormatSignedValue);
            AppendIntStat(builder, "EVA", skillEntry, levelNode, commonNode, infoNode, level, "eva", FormatSignedValue);
            AppendIntStat(builder, "Speed", skillEntry, levelNode, commonNode, infoNode, level, "speed", FormatSignedValue);
            AppendIntStat(builder, "Jump", skillEntry, levelNode, commonNode, infoNode, level, "jump", FormatSignedValue);
            AppendIntStat(builder, "HP Recovery", skillEntry, levelNode, commonNode, infoNode, level, "hp");
            AppendIntStat(builder, "MP Recovery", skillEntry, levelNode, commonNode, infoNode, level, "mp");
            AppendIntStat(builder, "Bullet Count", skillEntry, levelNode, commonNode, infoNode, level, "bulletCount");
            AppendIntStat(builder, "Bullet Speed", skillEntry, levelNode, commonNode, infoNode, level, "bulletSpeed");
            AppendIntStat(builder, "X", skillEntry, levelNode, commonNode, infoNode, level, "x");
            AppendIntStat(builder, "Y", skillEntry, levelNode, commonNode, infoNode, level, "y");
            AppendIntStat(builder, "Z", skillEntry, levelNode, commonNode, infoNode, level, "z");

            return builder.ToString().Trim();
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

        private static string NormalizeSkillText(string text, bool preserveFormatting = false)
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
            if (property?.WzProperties == null || property.WzProperties.Count < 2)
                return false;

            entries = new List<RecommendedSkillEntry>();
            foreach (WzImageProperty child in property.WzProperties)
            {
                if (child == null)
                    return false;

                if (!int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int spentSpThreshold) ||
                    spentSpThreshold < 0 ||
                    !TryGetIntValue(child, out int skillId) ||
                    !validSkillIds.Contains(skillId))
                {
                    return false;
                }

                entries.Add(new RecommendedSkillEntry(spentSpThreshold, skillId));
            }

            if (entries.Count < 2)
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
