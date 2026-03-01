using System;
using System.IO;
using System.Reflection;
using System.Text;
using MapleLib.WzLib.WzStructure.Data;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Builds and manages system prompts for the AI map editor.
    /// Handles loading prompts from files and replacing dynamic placeholders.
    /// </summary>
    public static class MapEditorPromptBuilder
    {
        /// <summary>
        /// Load the system prompt from external file and replace placeholders
        /// </summary>
        public static string LoadSystemPrompt()
        {
            string promptContent = null;

            // Try to load from external file first (deployed location)
            var externalPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
                "AI", "Prompts", "MapEditorSystemPrompt.txt");

            if (File.Exists(externalPath))
            {
                promptContent = File.ReadAllText(externalPath);
            }
            else
            {
                // Try source location (development)
                var sourcePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "MapEditor", "AI", "Prompts", "MapEditorSystemPrompt.txt");

                if (File.Exists(sourcePath))
                {
                    promptContent = File.ReadAllText(sourcePath);
                }
                else
                {
                    throw new FileNotFoundException(
                        $"System prompt file not found. Expected at:\n- {externalPath}\n- {sourcePath}");
                }
            }

            // Replace dynamic placeholders
            promptContent = ReplacePlaceholders(promptContent);

            return promptContent;
        }

        /// <summary>
        /// Replace all dynamic placeholders in the prompt content
        /// </summary>
        private static string ReplacePlaceholders(string promptContent)
        {
            promptContent = promptContent.Replace("{PORTAL_TYPES}", GeneratePortalTypesDocumentation());
            promptContent = promptContent.Replace("{BACKGROUND_TYPES}", GenerateBackgroundTypesDocumentation());
            promptContent = promptContent.Replace("{FIELD_LIMITS}", GenerateFieldLimitsDocumentation());
            promptContent = promptContent.Replace("{FIELD_TYPES}", GenerateFieldTypesDocumentation());
            return promptContent;
        }

        /// <summary>
        /// Generate portal types documentation dynamically from PortalType enum
        /// </summary>
        private static string GeneratePortalTypesDocumentation()
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Portal Types");
            sb.AppendLine();
            sb.AppendLine("Portal types control how the portal appears and behaves:");
            sb.AppendLine();

            foreach (PortalType portalType in Enum.GetValues(typeof(PortalType)))
            {
                try
                {
                    var code = portalType.ToCode();
                    var friendlyName = portalType.GetFriendlyName();
                    sb.AppendLine($"- `{portalType}` ({code}) = {friendlyName}");
                }
                catch
                {
                    // Skip if extension methods fail for any reason
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Generate background types documentation dynamically from BackgroundType enum
        /// </summary>
        private static string GenerateBackgroundTypesDocumentation()
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Background Behavior Types");
            sb.AppendLine();
            sb.AppendLine("The `background_type` parameter controls how the background tiles and moves:");
            sb.AppendLine();

            // Generate descriptions for each BackgroundType using extension method
            foreach (BackgroundType bgType in Enum.GetValues(typeof(BackgroundType)))
            {
                try
                {
                    string description = bgType.GetDescription();
                    sb.AppendLine($"- `{(int)bgType}` = **{bgType}**: {description}");
                }
                catch
                {
                    // Skip if extension method fails for any reason
                }
            }

            sb.AppendLine();
            sb.AppendLine("**Note:** If not specified, the system auto-determines the type based on cx/cy:");
            sb.AppendLine("- cx > 0 && cy > 0 → HVTiling (3)");
            sb.AppendLine("- cx > 0 only → HorizontalTiling (1)");
            sb.AppendLine("- cy > 0 only → VerticalTiling (2)");
            sb.AppendLine("- otherwise → Regular (0)");

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Generate field limits documentation dynamically from FieldLimitType enum
        /// </summary>
        private static string GenerateFieldLimitsDocumentation()
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Field Limits (Restrictions)");
            sb.AppendLine();
            sb.AppendLine("Field limits control what players can and cannot do on a map:");
            sb.AppendLine();

            // Group field limits by category for better organization
            var fieldLimitDescriptions = new System.Collections.Generic.Dictionary<FieldLimitType, (string category, string description)>
            {
                // Movement Restrictions
                { FieldLimitType.Unable_To_Jump, ("Movement", "Players cannot jump") },
                { FieldLimitType.Unable_To_Fall_Down, ("Movement", "Cannot fall off platforms") },
                { FieldLimitType.Move_Skill_Only, ("Movement", "Movement skills only") },

                // Skill & Item Restrictions
                { FieldLimitType.Unable_To_Use_Skill, ("Skill & Item", "Players cannot use skills") },
                { FieldLimitType.Unable_To_Use_Summon_Item, ("Skill & Item", "Cannot use summon items") },
                { FieldLimitType.Unable_To_Use_Mystic_Door, ("Skill & Item", "Cannot use Mystic Door") },
                { FieldLimitType.Unable_To_Consume_Stat_Change_Item, ("Skill & Item", "Cannot use stat potions") },
                { FieldLimitType.Unable_To_Use_Portal_Scroll, ("Skill & Item", "Cannot use portal scrolls") },
                { FieldLimitType.Unable_To_Use_Teleport_Item, ("Skill & Item", "Cannot use teleport items") },
                { FieldLimitType.Unable_To_Use_Specific_Portal_Scroll, ("Skill & Item", "Cannot use specific portal scrolls") },

                // Pet & Mount Restrictions
                { FieldLimitType.Unable_To_Use_Pet, ("Pet & Mount", "Cannot use pets") },
                { FieldLimitType.Unable_To_Use_Taming_Mob, ("Pet & Mount", "Cannot use mounts") },
                { FieldLimitType.No_Android, ("Pet & Mount", "Cannot use androids") },

                // Social Restrictions
                { FieldLimitType.Unable_To_Open_Mini_Game, ("Social", "Cannot open mini games") },
                { FieldLimitType.Unable_To_Change_Party_Boss, ("Social", "Cannot change party leader") },
                { FieldLimitType.Unable_To_Use_Wedding_Invitation_Item, ("Social", "Cannot use wedding items") },

                // Map Mechanics
                { FieldLimitType.Unable_To_Migrate, ("Map Mechanics", "Cannot change channels") },
                { FieldLimitType.Unable_To_Summon_NPC, ("Map Mechanics", "Cannot summon NPCs") },
                { FieldLimitType.No_Monster_Capacity_Limit, ("Map Mechanics", "No mob spawn limit") },
                { FieldLimitType.No_EXP_Decrease, ("Map Mechanics", "No EXP loss on death") },
                { FieldLimitType.No_Damage_On_Falling, ("Map Mechanics", "No fall damage") },
                { FieldLimitType.Drop_Limit, ("Map Mechanics", "Item drop restrictions") },
                { FieldLimitType.Parcel_Open_Limit, ("Map Mechanics", "Cannot open parcels") },

                // Cash & Special
                { FieldLimitType.Unable_To_Use_Cash_Weather, ("Cash & Special", "Cannot use cash weather items") },
                { FieldLimitType.Unable_To_Use_AntiMacro_Item, ("Cash & Special", "Cannot use anti-macro items") },
                { FieldLimitType.Unable_To_Use_Rocket_Boost, ("Cash & Special", "Cannot use rocket boost") },
                { FieldLimitType.No_Item_Option_Limit, ("Cash & Special", "No item option limits") },
                { FieldLimitType.No_Quest_Alert, ("Cash & Special", "No quest alerts") },
                { FieldLimitType.Auto_Expand_Minimap, ("Cash & Special", "Auto-expand minimap") },
            };

            // Group by category
            string currentCategory = "";
            foreach (FieldLimitType limitType in Enum.GetValues(typeof(FieldLimitType)))
            {
                if (fieldLimitDescriptions.TryGetValue(limitType, out var info))
                {
                    if (info.category != currentCategory)
                    {
                        if (!string.IsNullOrEmpty(currentCategory))
                            sb.AppendLine();
                        sb.AppendLine($"### {info.category} Restrictions");
                        currentCategory = info.category;
                    }
                    sb.AppendLine($"- `{limitType}`: {info.description}");
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Generate field types documentation dynamically from FieldType enum
        /// </summary>
        private static string GenerateFieldTypesDocumentation()
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Field Types");
            sb.AppendLine();
            sb.AppendLine("Field types define special map mechanics. Common types:");
            sb.AppendLine();

            // Filter to only include commonly used/relevant field types
            var commonFieldTypes = new System.Collections.Generic.Dictionary<FieldType, string>
            {
                { FieldType.FIELDTYPE_DEFAULT, "Normal field (default)" },
                { FieldType.FIELDTYPE_SNOWBALL, "Snowball event" },
                { FieldType.FIELDTYPE_CONTIMOVE, "Moving/scrolling field" },
                { FieldType.FIELDTYPE_TOURNAMENT, "Tournament/Massacre PQ type" },
                { FieldType.FIELDTYPE_COCONUT, "Coconut harvest event" },
                { FieldType.FIELDTYPE_OXQUIZ, "OX Quiz event" },
                { FieldType.FIELDTYPE_PERSONALTIMELIMIT, "Personal time limit map" },
                { FieldType.FIELDTYPE_WAITINGROOM, "Waiting room" },
                { FieldType.FIELDTYPE_GUILDBOSS, "Guild boss map" },
                { FieldType.FIELDTYPE_LIMITEDVIEW, "Limited view/fog map" },
                { FieldType.FIELDTYPE_MONSTERCARNIVAL_S2, "Monster Carnival (Season 2)" },
                { FieldType.FIELDTYPE_ZAKUM, "Zakum boss map" },
                { FieldType.FIELDTYPE_ARIANTARENA, "Ariant Arena PvP" },
                { FieldType.FIELDTYPE_DOJANG, "Dojang/Mu Lung Dojo" },
                { FieldType.FIELDTYPE_COOKIEHOUSE, "Cookie House event" },
                { FieldType.FIELDTYPE_BALROG, "Balrog boss map" },
                { FieldType.FIELDTYPE_BATTLEFIELD, "Battlefield PvP" },
                { FieldType.FIELDTYPE_TUTORIAL, "Tutorial map" },
                { FieldType.FIELDTYPE_MASSACRE, "Massacre mode" },
                { FieldType.FIELDTYPE_PARTYRAID, "Party raid" },
                { FieldType.FIELDTYPE_ESCORT, "Escort mission" },
                { FieldType.FIELDTYPE_CHAOSZAKUM, "Chaos Zakum boss" },
                { FieldType.FIELDTYPE_KILLCOUNT, "Kill count tracking" },
                { FieldType.FIELDTYPE_PVP, "PvP map" },
                { FieldType.FIELDTYPE_WEDDING, "Wedding ceremony" },
                { FieldType.FIELDTYPE_URUS, "Urus boss" },
            };

            foreach (var kvp in commonFieldTypes)
            {
                sb.AppendLine($"- {kvp.Key} ({(int)kvp.Key}): {kvp.Value}");
            }

            sb.AppendLine();
            sb.AppendLine("### All Available Field Types");
            sb.AppendLine("For less common types, here's the complete list:");
            sb.AppendLine();

            // List all field types with their numeric values
            foreach (FieldType fieldType in Enum.GetValues(typeof(FieldType)))
            {
                // Skip the COUNT_OF_FIELDTYPE marker
                if (fieldType == FieldType.COUNT_OF_FIELDTYPE)
                    continue;

                var name = fieldType.ToString().Replace("FIELDTYPE_", "").Replace("FILEDTYPE_", "");
                sb.AppendLine($"- {fieldType} ({(int)fieldType}): {FormatFieldTypeName(name)}");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Format a field type name to be more readable
        /// </summary>
        private static string FormatFieldTypeName(string name)
        {
            // Convert SCREAMING_CASE to Title Case
            var words = name.Split('_');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }
            return string.Join(" ", words);
        }

        /// <summary>
        /// Load an agent-specific prompt file with placeholder replacement
        /// </summary>
        /// <param name="agentType">The agent type (e.g., "settings", "background", "platform")</param>
        /// <returns>The processed prompt content</returns>
        public static string LoadAgentPrompt(string agentType)
        {
            var promptFileName = $"{char.ToUpper(agentType[0])}{agentType.Substring(1)}AgentPrompt.txt";
            return LoadPromptFile(promptFileName);
        }

        /// <summary>
        /// Load a prompt file with placeholder replacement
        /// </summary>
        /// <param name="fileName">The prompt file name</param>
        /// <returns>The processed prompt content</returns>
        public static string LoadPromptFile(string fileName)
        {
            string promptContent = null;

            // Try to load from external file first (deployed location)
            var externalPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
                "AI", "Prompts", fileName);

            if (File.Exists(externalPath))
            {
                promptContent = File.ReadAllText(externalPath);
            }
            else
            {
                // Try source location (development)
                var sourcePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "MapEditor", "AI", "Prompts", fileName);

                if (File.Exists(sourcePath))
                {
                    promptContent = File.ReadAllText(sourcePath);
                }
                else
                {
                    throw new FileNotFoundException($"Prompt file not found: {fileName}");
                }
            }

            // Replace dynamic placeholders
            promptContent = ReplacePlaceholders(promptContent);

            return promptContent;
        }

        /// <summary>
        /// Build the user message with map context and instructions
        /// </summary>
        public static string BuildUserMessage(string mapContext, string userInstructions)
        {
            return $@"## Current Map State
{mapContext}

## User Request
{userInstructions}

Use the available functions to fulfill the user's request. Call multiple functions as needed.
IMPORTANT: For objects and backgrounds, call get_object_info or get_background_info FIRST to discover valid paths, then use add_object or add_background with those exact paths.";
        }
    }
}
