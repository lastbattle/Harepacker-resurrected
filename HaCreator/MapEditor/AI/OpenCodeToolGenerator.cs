using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Generates OpenCode TypeScript tool files from C# tool definitions.
    /// This ensures a single source of truth - define tools in C#, generate TypeScript.
    /// </summary>
    public static class OpenCodeToolGenerator
    {
        private const string TOOL_IMPORT = "@opencode-ai/plugin/tool";

        /// <summary>
        /// Generate all TypeScript tool files to the specified directory.
        /// </summary>
        /// <param name="outputDir">Directory to write .ts files (e.g., ".opencode/tool")</param>
        /// <returns>Number of files generated</returns>
        public static int GenerateAllTools(string outputDir)
        {
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Generate the bridge file first
            GenerateBridgeFile(outputDir);

            var tools = MapEditorFunctions.GetToolDefinitions();
            int count = 0;

            foreach (JObject tool in tools)
            {
                var func = tool["function"] as JObject;
                if (func == null) continue;

                var name = func["name"]?.ToString();
                if (string.IsNullOrEmpty(name)) continue;

                var tsContent = GenerateToolFile(func);
                var filePath = Path.Combine(outputDir, $"{name}.ts");
                File.WriteAllText(filePath, tsContent, Encoding.UTF8);
                count++;
            }

            return count;
        }

        /// <summary>
        /// Generate a single TypeScript tool file content using Zod schemas.
        /// The @opencode-ai/plugin uses Zod, not JSON Schema.
        /// </summary>
        private static string GenerateToolFile(JObject func)
        {
            var name = func["name"]?.ToString();
            var description = func["description"]?.ToString() ?? "";
            var parameters = func["parameters"] as JObject;

            var sb = new StringBuilder();

            // Imports
            sb.AppendLine($"import {{ tool }} from \"{TOOL_IMPORT}\";");
            sb.AppendLine("import { callHarepacker } from \"./harepacker-bridge\";");
            sb.AppendLine();

            // Tool definition using Zod schema format
            sb.AppendLine("export default tool({");
            sb.AppendLine($"  description: {EscapeString(description)},");

            // Args using Zod schemas
            var properties = parameters?["properties"] as JObject;
            var required = parameters?["required"] as JArray;
            var requiredSet = new HashSet<string>(required?.Select(r => r.ToString()) ?? Enumerable.Empty<string>());

            if (properties != null && properties.Count > 0)
            {
                sb.AppendLine("  args: {");
                var propList = properties.Properties().ToList();
                for (int i = 0; i < propList.Count; i++)
                {
                    var prop = propList[i];
                    var propObj = prop.Value as JObject;
                    var isLast = i == propList.Count - 1;
                    var isRequired = requiredSet.Contains(prop.Name);
                    sb.Append(GenerateZodProperty(prop.Name, propObj, isRequired, 4));
                    sb.AppendLine(isLast ? "" : ",");
                }
                sb.AppendLine("  },");
            }
            else
            {
                sb.AppendLine("  args: {},");
            }

            // Execute function with context parameter
            sb.AppendLine("  async execute(args, context) {");
            sb.AppendLine($"    const result = await callHarepacker(\"{name}\", args);");
            sb.AppendLine("    if (!result.success) return `Error: ${result.error}`;");
            sb.AppendLine($"    return result.data?.message ?? \"Command executed: {name}\";");
            sb.AppendLine("  },");

            sb.AppendLine("});");

            return sb.ToString();
        }

        /// <summary>
        /// Generate a Zod schema property.
        /// </summary>
        private static string GenerateZodProperty(string name, JObject prop, bool isRequired, int indent)
        {
            var spaces = new string(' ', indent);
            var type = prop["type"]?.ToString() ?? "string";
            var desc = prop["description"]?.ToString();
            var enumValues = prop["enum"] as JArray;

            string zodType;

            if (enumValues != null && enumValues.Count > 0)
            {
                // Enum type
                var enumList = string.Join(", ", enumValues.Select(e => $"\"{e}\""));
                zodType = $"tool.schema.enum([{enumList}])";
            }
            else
            {
                // Basic types
                zodType = type switch
                {
                    "integer" => "tool.schema.number().int()",
                    "number" => "tool.schema.number()",
                    "boolean" => "tool.schema.boolean()",
                    "array" => GenerateZodArrayType(prop),
                    _ => "tool.schema.string()"
                };
            }

            // Add description
            if (!string.IsNullOrEmpty(desc))
            {
                zodType += $".describe({EscapeString(desc)})";
            }

            // Make optional if not required
            if (!isRequired)
            {
                zodType += ".optional()";
            }

            return $"{spaces}{name}: {zodType}";
        }

        /// <summary>
        /// Generate Zod array type.
        /// </summary>
        private static string GenerateZodArrayType(JObject prop)
        {
            var items = prop["items"] as JObject;
            if (items == null)
            {
                return "tool.schema.array(tool.schema.any())";
            }

            var itemType = items["type"]?.ToString() ?? "string";
            var innerType = itemType switch
            {
                "integer" => "tool.schema.number().int()",
                "number" => "tool.schema.number()",
                "boolean" => "tool.schema.boolean()",
                "object" => GenerateZodObjectType(items),
                _ => "tool.schema.string()"
            };

            return $"tool.schema.array({innerType})";
        }

        /// <summary>
        /// Generate Zod object type for nested objects.
        /// </summary>
        private static string GenerateZodObjectType(JObject obj)
        {
            var properties = obj["properties"] as JObject;
            if (properties == null || properties.Count == 0)
            {
                return "tool.schema.object({})";
            }

            var required = obj["required"] as JArray;
            var requiredSet = new HashSet<string>(required?.Select(r => r.ToString()) ?? Enumerable.Empty<string>());

            var propParts = new List<string>();
            foreach (var prop in properties.Properties())
            {
                var propObj = prop.Value as JObject;
                var isRequired = requiredSet.Contains(prop.Name);
                var type = propObj?["type"]?.ToString() ?? "string";
                var desc = propObj?["description"]?.ToString();

                var zodType = type switch
                {
                    "integer" => "tool.schema.number().int()",
                    "number" => "tool.schema.number()",
                    "boolean" => "tool.schema.boolean()",
                    _ => "tool.schema.string()"
                };

                if (!string.IsNullOrEmpty(desc))
                {
                    zodType += $".describe({EscapeString(desc)})";
                }

                if (!isRequired)
                {
                    zodType += ".optional()";
                }

                propParts.Add($"{prop.Name}: {zodType}");
            }

            return $"tool.schema.object({{ {string.Join(", ", propParts)} }})";
        }

        /// <summary>
        /// Escape a string for TypeScript.
        /// </summary>
        private static string EscapeString(string s)
        {
            if (s == null) return "\"\"";

            // Use template literal for multi-line or complex strings
            if (s.Contains("\n") || s.Contains("\""))
            {
                return $"`{s.Replace("`", "\\`").Replace("${", "\\${")}`";
            }

            return $"\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        }

        /// <summary>
        /// Generate the bridge file.
        /// </summary>
        private static void GenerateBridgeFile(string outputDir)
        {
            var content = @"/**
 * Bridge module for communicating with HaCreator/Harepacker C# application.
 * All map editing tools use this to send commands to the running application.
 *
 * AUTO-GENERATED - Do not edit manually!
 * Regenerate with: MapEditorFunctions.RegenerateOpenCodeTools()
 */

const HAREPACKER_PORT = 19840;
const HAREPACKER_HOST = ""127.0.0.1"";

export interface ToolResult {
  success: boolean;
  data?: any;
  error?: string;
}

/**
 * Send a tool command to the HaCreator application.
 */
export async function callHarepacker(toolName: string, args: Record<string, any>): Promise<ToolResult> {
  const url = `http://${HAREPACKER_HOST}:${HAREPACKER_PORT}/tool`;

  try {
    const response = await fetch(url, {
      method: ""POST"",
      headers: { ""Content-Type"": ""application/json"" },
      body: JSON.stringify({ tool: toolName, arguments: args }),
    });

    if (!response.ok) {
      const errorText = await response.text();
      return { success: false, error: `HTTP ${response.status}: ${errorText}` };
    }

    const result = await response.json();
    return { success: true, data: result };
  } catch (error: any) {
    return {
      success: false,
      error: `Connection failed: ${error.message}. Is HaCreator running with AI Tool Server enabled?`,
    };
  }
}
";
            var filePath = Path.Combine(outputDir, "harepacker-bridge.ts");
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }

        /// <summary>
        /// Regenerate all OpenCode tools in the default location (.opencode/tool).
        /// Call this when tool definitions change.
        /// </summary>
        /// <param name="projectRoot">Root directory of the project (where .opencode folder is)</param>
        /// <returns>Number of tools generated</returns>
        public static int RegenerateTools(string projectRoot = null)
        {
            if (string.IsNullOrEmpty(projectRoot))
            {
                // Try to find project root from current directory
                projectRoot = FindProjectRoot();
            }

            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException("Could not find project root. Pass the path explicitly.");
            }

            var toolDir = Path.Combine(projectRoot, ".opencode", "tool");
            return GenerateAllTools(toolDir);
        }

        /// <summary>
        /// Find the project root by looking for .opencode folder.
        /// </summary>
        private static string FindProjectRoot()
        {
            var dir = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(Path.Combine(dir, ".opencode")))
                {
                    return dir;
                }
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return null;
        }

        /// <summary>
        /// Auto-regenerate tools if needed (called on app startup).
        /// Only regenerates if tool definitions have changed since last generation.
        /// </summary>
        /// <param name="projectRoot">Project root path</param>
        /// <returns>True if tools were regenerated, false if skipped</returns>
        public static bool AutoRegenerateIfNeeded(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot))
                projectRoot = FindProjectRoot();

            if (string.IsNullOrEmpty(projectRoot))
                return false;

            var toolDir = Path.Combine(projectRoot, ".opencode", "tool");
            var hashFile = Path.Combine(toolDir, ".generated_hash");

            // Calculate hash of current tool definitions
            var currentHash = CalculateToolDefinitionsHash();

            // Check if we need to regenerate
            if (File.Exists(hashFile))
            {
                var storedHash = File.ReadAllText(hashFile).Trim();
                if (storedHash == currentHash)
                {
                    // No changes, skip regeneration
                    return false;
                }
            }

            // Regenerate tools
            try
            {
                GenerateAllTools(toolDir);

                // Store the hash
                File.WriteAllText(hashFile, currentHash);
                return true;
            }
            catch
            {
                // Silently fail - tools may still be usable
                return false;
            }
        }

        /// <summary>
        /// Calculate a hash of all tool definitions for change detection.
        /// </summary>
        private static string CalculateToolDefinitionsHash()
        {
            try
            {
                var tools = MapEditorFunctions.GetToolDefinitions();
                var json = tools.ToString(Newtonsoft.Json.Formatting.None);

                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                    var hash = sha.ComputeHash(bytes);
                    return Convert.ToBase64String(hash).Substring(0, 16);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OpenCodeToolGenerator] Hash calculation failed: {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }
    }
}
