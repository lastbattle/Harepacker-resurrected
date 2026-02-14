using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace HaCreator.MapSimulator.Loaders
{
    /// <summary>
    /// Loads skill data from Skill.wz and String.wz for displaying in the skill window
    /// </summary>
    public static class SkillDataLoader
    {
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
            Texture2D iconTexture = iconBitmap?.ToTexture2D(device);
            if (iconTexture == null)
                return null;

            // Get disabled icon if available
            Texture2D disabledIconTexture = null;
            WzCanvasProperty disabledIconProp = (WzCanvasProperty)skillEntry["iconDisabled"];
            if (disabledIconProp != null)
            {
                System.Drawing.Bitmap disabledBitmap = disabledIconProp.GetLinkedWzCanvasBitmap();
                disabledIconTexture = disabledBitmap?.ToTexture2D(device);
            }

            // Get mouse over icon if available
            Texture2D mouseOverIconTexture = null;
            WzCanvasProperty mouseOverIconProp = (WzCanvasProperty)skillEntry["iconMouseOver"];
            if (mouseOverIconProp != null)
            {
                System.Drawing.Bitmap mouseOverBitmap = mouseOverIconProp.GetLinkedWzCanvasBitmap();
                mouseOverIconTexture = mouseOverBitmap?.ToTexture2D(device);
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
                }
            }

            return new SkillDisplayData
            {
                SkillId = skillId,
                IconTexture = iconTexture,
                IconDisabledTexture = disabledIconTexture,
                IconMouseOverTexture = mouseOverIconTexture,
                SkillName = skillName,
                Description = description,
                CurrentLevel = 1, // Default to level 1 for display purposes
                MaxLevel = Math.Max(1, maxLevel)
            };
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
            return iconBitmap?.ToTexture2D(device);
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
    }
}
