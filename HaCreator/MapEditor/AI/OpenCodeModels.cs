using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    #region Session Models

    /// <summary>
    /// Information about an OpenCode session
    /// </summary>
    public class OpenCodeSessionInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }
    }

    #endregion

    #region Model Configuration

    /// <summary>
    /// Model specification for OpenCode requests
    /// </summary>
    public class OpenCodeModelSpec
    {
        [JsonProperty("providerID")]
        public string ProviderId { get; set; }

        [JsonProperty("modelID")]
        public string ModelId { get; set; }

        /// <summary>
        /// Parse a model string like "anthropic/claude-sonnet-4-20250514" into provider/model parts
        /// </summary>
        public static OpenCodeModelSpec Parse(string model)
        {
            if (string.IsNullOrEmpty(model))
                return null;

            if (model.Contains("/"))
            {
                var parts = model.Split(new[] { '/' }, 2);
                return new OpenCodeModelSpec
                {
                    ProviderId = parts[0],
                    ModelId = parts[1]
                };
            }

            return new OpenCodeModelSpec
            {
                ProviderId = "anthropic",
                ModelId = model
            };
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["providerID"] = ProviderId,
                ["modelID"] = ModelId
            };
        }
    }

    /// <summary>
    /// Configuration for extended thinking (Claude models)
    /// </summary>
    public class ThinkingConfig
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "enabled";

        [JsonProperty("budgetTokens")]
        public int BudgetTokens { get; set; }

        public JObject ToJson()
        {
            return new JObject
            {
                ["type"] = Type,
                ["budgetTokens"] = BudgetTokens
            };
        }
    }

    #endregion

    #region Message Parts

    /// <summary>
    /// A part of a message (text, file, tool_use, tool_result, etc.)
    /// </summary>
    public class OpenCodeMessagePart
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        // For text parts
        [JsonProperty("text")]
        public string Text { get; set; }

        // For file/image parts
        [JsonProperty("mime")]
        public string Mime { get; set; }

        [JsonProperty("filename")]
        public string Filename { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        // For tool_use parts
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("input")]
        public JObject Input { get; set; }

        [JsonProperty("args")]
        public JObject Args { get; set; }

        [JsonProperty("arguments")]
        public JObject Arguments { get; set; }

        /// <summary>
        /// Get the arguments from whichever field contains them
        /// </summary>
        public JObject GetArguments()
        {
            return Input ?? Args ?? Arguments ?? new JObject();
        }

        /// <summary>
        /// Create a text message part
        /// </summary>
        public static OpenCodeMessagePart CreateText(string text)
        {
            return new OpenCodeMessagePart
            {
                Type = "text",
                Text = text
            };
        }

        /// <summary>
        /// Create a file/image message part
        /// </summary>
        public static OpenCodeMessagePart CreateFile(string filePath, string mimeType = null)
        {
            var absPath = System.IO.Path.GetFullPath(filePath);
            mimeType = mimeType ?? GetMimeType(filePath);

            return new OpenCodeMessagePart
            {
                Type = "file",
                Mime = mimeType,
                Filename = System.IO.Path.GetFileName(filePath),
                Url = $"file://{System.Web.HttpUtility.UrlEncode(absPath)}"
            };
        }

        /// <summary>
        /// Create a tool result message part
        /// </summary>
        public static OpenCodeMessagePart CreateToolResult(string toolCallId, string content, bool isError = false)
        {
            var part = new OpenCodeMessagePart
            {
                Type = "tool_result",
                Id = toolCallId
            };

            // Store in a way that can be serialized properly
            part.Text = content;
            return part;
        }

        private static string GetMimeType(string filePath)
        {
            var ext = System.IO.Path.GetExtension(filePath)?.ToLowerInvariant();
            switch (ext)
            {
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".gif": return "image/gif";
                case ".webp": return "image/webp";
                case ".pdf": return "application/pdf";
                case ".txt": return "text/plain";
                case ".csv": return "text/csv";
                case ".json": return "application/json";
                default: return "application/octet-stream";
            }
        }

        public JObject ToJson()
        {
            var obj = new JObject { ["type"] = Type };

            if (!string.IsNullOrEmpty(Text))
                obj["text"] = Text;
            if (!string.IsNullOrEmpty(Mime))
                obj["mime"] = Mime;
            if (!string.IsNullOrEmpty(Filename))
                obj["filename"] = Filename;
            if (!string.IsNullOrEmpty(Url))
                obj["url"] = Url;
            if (!string.IsNullOrEmpty(Id))
            {
                if (Type == "tool_result")
                {
                    obj["tool_use_id"] = Id;
                    obj["content"] = Text;
                }
                else
                {
                    obj["id"] = Id;
                }
            }
            if (!string.IsNullOrEmpty(Name))
                obj["name"] = Name;
            if (Input != null)
                obj["input"] = Input;

            return obj;
        }
    }

    #endregion

    #region Tool Call Models

    /// <summary>
    /// Represents a tool call extracted from an OpenCode response
    /// </summary>
    public class OpenCodeToolCall
    {
        /// <summary>
        /// Unique identifier for this tool call (used for tool_result)
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Name of the tool/function being called
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Arguments passed to the tool (already parsed as dictionary)
        /// </summary>
        public Dictionary<string, object> Arguments { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Raw arguments as JObject for direct manipulation
        /// </summary>
        public JObject RawArguments { get; set; }
    }

    /// <summary>
    /// Result of executing a tool call
    /// </summary>
    public class OpenCodeToolCallResult
    {
        /// <summary>
        /// Name of the tool that was called
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Arguments that were passed
        /// </summary>
        public Dictionary<string, object> Arguments { get; set; }

        /// <summary>
        /// Result of the tool execution
        /// </summary>
        public object Result { get; set; }

        /// <summary>
        /// Whether the tool execution resulted in an error
        /// </summary>
        public bool IsError { get; set; }
    }

    #endregion

    #region Response Models

    /// <summary>
    /// Token usage information from the response
    /// </summary>
    public class OpenCodeTokenInfo
    {
        [JsonProperty("input")]
        public int Input { get; set; }

        [JsonProperty("output")]
        public int Output { get; set; }
    }

    /// <summary>
    /// Response metadata from OpenCode
    /// </summary>
    public class OpenCodeResponseInfo
    {
        [JsonProperty("modelID")]
        public string ModelId { get; set; }

        [JsonProperty("providerID")]
        public string ProviderId { get; set; }

        [JsonProperty("tokens")]
        public OpenCodeTokenInfo Tokens { get; set; }

        [JsonProperty("finish")]
        public string Finish { get; set; }
    }

    /// <summary>
    /// Full response from an OpenCode API call
    /// </summary>
    public class OpenCodeResponse
    {
        [JsonProperty("info")]
        public OpenCodeResponseInfo Info { get; set; }

        [JsonProperty("parts")]
        public List<OpenCodeMessagePart> Parts { get; set; } = new List<OpenCodeMessagePart>();

        [JsonProperty("error")]
        public string Error { get; set; }

        /// <summary>
        /// Extract all text content from the response
        /// </summary>
        public string GetTextContent()
        {
            if (Parts == null || Parts.Count == 0)
                return string.Empty;

            var texts = new List<string>();
            foreach (var part in Parts)
            {
                if (part.Type == "text" && !string.IsNullOrEmpty(part.Text))
                {
                    texts.Add(part.Text);
                }
            }

            return string.Join("\n", texts);
        }

        /// <summary>
        /// Check if the response contains tool calls
        /// </summary>
        public bool HasToolCalls()
        {
            if (Parts == null) return false;
            foreach (var part in Parts)
            {
                if (part.Type == "tool_use" || part.Type == "tool-invocation")
                    return true;
            }
            return false;
        }
    }

    #endregion

    #region RunWithTools Result

    /// <summary>
    /// Result from the high-level RunWithToolsAsync method
    /// </summary>
    public class RunWithToolsResult
    {
        /// <summary>
        /// Final text response from the model
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// List of all tool calls that were made during the conversation
        /// </summary>
        public List<OpenCodeToolCallResult> ToolCallsMade { get; set; } = new List<OpenCodeToolCallResult>();

        /// <summary>
        /// Number of conversation turns (iterations) used
        /// </summary>
        public int Iterations { get; set; }

        /// <summary>
        /// Whether the execution completed successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if execution failed
        /// </summary>
        public string Error { get; set; }
    }

    #endregion
}
