using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaRepacker.GUI
{
    /// <summary>
    /// Manages category-based tabs and intelligent file loading
    /// </summary>
    public class CategoryTabManager
    {
        public enum FileCategory
        {
            Skills,
            Quests,
            Maps,
            Items,
            UI,
            Characters,
            Sounds,
            Custom
        }

        /// <summary>
        /// Defines which files belong to each category
        /// </summary>
        private static readonly Dictionary<FileCategory, List<string>> CategoryFileMapping = new Dictionary<FileCategory, List<string>>
        {
            { FileCategory.Skills, new List<string> { "Skill", "String/Skill" } },
            { FileCategory.Quests, new List<string> { "Quest", "Mob", "Npc", "String/Mob", "String/Npc" } },
            { FileCategory.Maps, new List<string> { "Map/Map", "Map/Tile", "Map/Obj", "Map/Back" } },
            { FileCategory.Items, new List<string> { "Item", "Character", "String/Item", "String/Eqp" } },
            { FileCategory.UI, new List<string> { "UI" } },
            { FileCategory.Characters, new List<string> { "Character", "String/Character" } },
            { FileCategory.Sounds, new List<string> { "Sound" } }
        };

        /// <summary>
        /// Get files that should be loaded for a specific category
        /// </summary>
        public static List<string> GetFilesForCategory(FileCategory category, string basePath)
        {
            List<string> filesToLoad = new List<string>();
            
            if (!CategoryFileMapping.ContainsKey(category))
                return filesToLoad;

            foreach (string pattern in CategoryFileMapping[category])
            {
                // Check for .wz files
                string wzPath = Path.Combine(basePath, pattern + ".wz");
                if (File.Exists(wzPath))
                {
                    filesToLoad.Add(wzPath);
                }

                // Check for directory with .img files
                string dirPath = Path.Combine(basePath, pattern.Replace("/", "\\"));
                if (Directory.Exists(dirPath))
                {
                    string[] imgFiles = Directory.GetFiles(dirPath, "*.img", SearchOption.AllDirectories);
                    filesToLoad.AddRange(imgFiles);
                }
            }

            return filesToLoad;
        }

        /// <summary>
        /// Auto-detect category from file path
        /// </summary>
        public static FileCategory DetectCategory(string filePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath).ToLower();
            string dirName = Path.GetDirectoryName(filePath)?.ToLower() ?? "";

            if (fileName.Contains("skill") || dirName.Contains("skill"))
                return FileCategory.Skills;
            if (fileName.Contains("quest") || fileName.Contains("mob") || fileName.Contains("npc"))
                return FileCategory.Quests;
            if (fileName.Contains("map") || dirName.Contains("map"))
                return FileCategory.Maps;
            if (fileName.Contains("item") || dirName.Contains("item"))
                return FileCategory.Items;
            if (fileName.Contains("ui") || dirName.Contains("ui"))
                return FileCategory.UI;
            if (fileName.Contains("character"))
                return FileCategory.Characters;
            if (fileName.Contains("sound"))
                return FileCategory.Sounds;

            return FileCategory.Custom;
        }

        /// <summary>
        /// Get related files that should be loaded together
        /// </summary>
        public static List<string> GetRelatedFiles(string filePath, string basePath)
        {
            FileCategory category = DetectCategory(filePath);
            return GetFilesForCategory(category, basePath);
        }

        /// <summary>
        /// Get a friendly display name for the category
        /// </summary>
        public static string GetCategoryDisplayName(FileCategory category)
        {
            return category.ToString();
        }

        /// <summary>
        /// Get description of what files will be loaded for a category
        /// </summary>
        public static string GetCategoryDescription(FileCategory category)
        {
            if (!CategoryFileMapping.ContainsKey(category))
                return "Custom files";

            return string.Join(", ", CategoryFileMapping[category]);
        }
    }
}