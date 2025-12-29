/* Copyright (C) 2024 HaCreator AI Extension
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

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
