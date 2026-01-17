using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Represents a single tool/function definition for AI function calling.
    /// This is the canonical format used internally.
    /// </summary>
    public class AIToolDefinition
    {
        /// <summary>
        /// The unique name of the tool (e.g., "add_mob", "get_npc_list")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Human-readable description of what the tool does
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// JSON Schema for the parameters (type, properties, required)
        /// </summary>
        public JObject Parameters { get; set; }

        public AIToolDefinition() { }

        public AIToolDefinition(string name, string description, JObject parameters)
        {
            Name = name;
            Description = description;
            Parameters = parameters;
        }

        /// <summary>
        /// Create from OpenAI format: {"type": "function", "function": {"name": ..., "description": ..., "parameters": ...}}
        /// </summary>
        public static AIToolDefinition FromOpenAIFormat(JObject openAITool)
        {
            if (openAITool["type"]?.ToString() == "function" && openAITool["function"] != null)
            {
                var func = openAITool["function"] as JObject;
                return new AIToolDefinition
                {
                    Name = func?["name"]?.ToString(),
                    Description = func?["description"]?.ToString(),
                    Parameters = func?["parameters"] as JObject
                };
            }

            // Already in simple format
            return new AIToolDefinition
            {
                Name = openAITool["name"]?.ToString(),
                Description = openAITool["description"]?.ToString(),
                Parameters = openAITool["parameters"] as JObject
            };
        }
    }

    /// <summary>
    /// Collection of tool definitions with conversion methods for different AI providers.
    /// </summary>
    public class AIToolCollection
    {
        private readonly List<AIToolDefinition> _tools = new List<AIToolDefinition>();

        /// <summary>
        /// All tools in the collection
        /// </summary>
        public IReadOnlyList<AIToolDefinition> Tools => _tools.AsReadOnly();

        /// <summary>
        /// Number of tools in the collection
        /// </summary>
        public int Count => _tools.Count;

        /// <summary>
        /// Add a tool definition
        /// </summary>
        public void Add(AIToolDefinition tool)
        {
            _tools.Add(tool);
        }

        /// <summary>
        /// Add a tool from OpenAI format
        /// </summary>
        public void AddFromOpenAI(JObject openAITool)
        {
            _tools.Add(AIToolDefinition.FromOpenAIFormat(openAITool));
        }

        /// <summary>
        /// Add multiple tools from a JArray (OpenAI format)
        /// </summary>
        public void AddRangeFromOpenAI(JArray openAITools)
        {
            foreach (var tool in openAITools)
            {
                if (tool is JObject toolObj)
                {
                    AddFromOpenAI(toolObj);
                }
            }
        }

        /// <summary>
        /// Create a collection from OpenAI format JArray
        /// </summary>
        public static AIToolCollection FromOpenAIFormat(JArray openAITools)
        {
            var collection = new AIToolCollection();
            collection.AddRangeFromOpenAI(openAITools);
            return collection;
        }
    }

    /// <summary>
    /// Converts tool definitions between different AI provider formats.
    /// </summary>
    public static class AIToolConverter
    {
        /// <summary>
        /// Convert tools to OpenAI/OpenRouter format (array of function objects).
        /// Format: [{"type": "function", "function": {"name": ..., "description": ..., "parameters": ...}}, ...]
        /// </summary>
        public static JArray ToOpenAIFormat(AIToolCollection tools)
        {
            var result = new JArray();
            foreach (var tool in tools.Tools)
            {
                result.Add(new JObject
                {
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = tool.Parameters
                    }
                });
            }
            return result;
        }

        /// <summary>
        /// Convert tools to OpenAI/OpenRouter format from a JArray.
        /// If already in OpenAI format, returns as-is.
        /// </summary>
        public static JArray ToOpenAIFormat(JArray tools)
        {
            // Check if already in OpenAI format
            if (tools.Count > 0 && tools[0]["type"]?.ToString() == "function")
            {
                return tools;
            }

            // Convert from simple format
            var result = new JArray();
            foreach (var tool in tools)
            {
                result.Add(new JObject
                {
                    ["type"] = "function",
                    ["function"] = tool
                });
            }
            return result;
        }

        /// <summary>
        /// Convert tools to OpenCode format (object with tool names as keys).
        /// Format: {"tool_name": {"description": ..., "parameters": ...}, ...}
        /// </summary>
        public static JObject ToOpenCodeFormat(AIToolCollection tools)
        {
            var result = new JObject();
            foreach (var tool in tools.Tools)
            {
                if (!string.IsNullOrEmpty(tool.Name))
                {
                    var toolDef = new JObject();
                    if (!string.IsNullOrEmpty(tool.Description))
                        toolDef["description"] = tool.Description;
                    if (tool.Parameters != null)
                        toolDef["parameters"] = tool.Parameters;
                    result[tool.Name] = toolDef;
                }
            }
            return result;
        }

        /// <summary>
        /// Convert tools to OpenCode format from a JArray (OpenAI or simple format).
        /// Format: {"tool_name": {"description": ..., "parameters": ...}, ...}
        /// </summary>
        public static JObject ToOpenCodeFormat(JArray tools)
        {
            var result = new JObject();
            foreach (var tool in tools)
            {
                // Handle OpenAI format: {"type": "function", "function": {...}}
                JToken toolDef = tool;
                if (tool["type"]?.ToString() == "function" && tool["function"] != null)
                {
                    toolDef = tool["function"];
                }

                var toolName = toolDef["name"]?.ToString();
                if (!string.IsNullOrEmpty(toolName))
                {
                    var def = new JObject();
                    if (toolDef["description"] != null)
                        def["description"] = toolDef["description"];
                    if (toolDef["parameters"] != null)
                        def["parameters"] = toolDef["parameters"];
                    result[toolName] = def;
                }
            }
            return result;
        }

        /// <summary>
        /// Convert tools to simple format (array without OpenAI wrapper).
        /// Format: [{"name": ..., "description": ..., "parameters": ...}, ...]
        /// </summary>
        public static JArray ToSimpleFormat(AIToolCollection tools)
        {
            var result = new JArray();
            foreach (var tool in tools.Tools)
            {
                result.Add(new JObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = tool.Parameters
                });
            }
            return result;
        }

        /// <summary>
        /// Convert tools to simple format from a JArray.
        /// Extracts function definitions from OpenAI format if needed.
        /// </summary>
        public static JArray ToSimpleFormat(JArray tools)
        {
            var result = new JArray();
            foreach (var tool in tools)
            {
                if (tool["type"]?.ToString() == "function" && tool["function"] != null)
                {
                    // OpenAI format - extract function definition
                    result.Add(tool["function"]);
                }
                else
                {
                    // Already simple format
                    result.Add(tool);
                }
            }
            return result;
        }

        /// <summary>
        /// Convert tools to OpenCode boolean format.
        /// OpenCode expects: {"tool_name": true, ...} to enable tools by name.
        /// The actual tool definitions must be registered on the server side.
        /// </summary>
        public static JObject ToOpenCodeBooleanFormat(AIToolCollection tools)
        {
            var result = new JObject();
            foreach (var tool in tools.Tools)
            {
                if (!string.IsNullOrEmpty(tool.Name))
                {
                    result[tool.Name] = true;
                }
            }
            return result;
        }

        /// <summary>
        /// Convert tools to OpenCode boolean format from a JArray.
        /// OpenCode expects: {"tool_name": true, ...} to enable tools by name.
        /// </summary>
        public static JObject ToOpenCodeBooleanFormat(JArray tools)
        {
            var result = new JObject();
            foreach (var tool in tools)
            {
                // Handle OpenAI format: {"type": "function", "function": {...}}
                JToken toolDef = tool;
                if (tool["type"]?.ToString() == "function" && tool["function"] != null)
                {
                    toolDef = tool["function"];
                }

                var toolName = toolDef["name"]?.ToString();
                if (!string.IsNullOrEmpty(toolName))
                {
                    result[toolName] = true;
                }
            }
            return result;
        }
    }
}
