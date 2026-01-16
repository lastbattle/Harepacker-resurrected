using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Represents a task assigned to a specialized agent
    /// </summary>
    public class AgentTask
    {
        public string Agent { get; set; }
        public string Task { get; set; }
        public int Priority { get; set; }
    }

    /// <summary>
    /// Result from the orchestrator's planning phase
    /// </summary>
    public class OrchestrationPlan
    {
        public List<AgentTask> Agents { get; set; } = new List<AgentTask>();
        public string Reasoning { get; set; }
    }

    /// <summary>
    /// Result from executing a single agent
    /// </summary>
    public class AgentResult
    {
        public string AgentType { get; set; }
        public string Task { get; set; }
        public List<string> Commands { get; set; } = new List<string>();
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Orchestrates multiple specialized AI agents for layer-by-layer map editing.
    /// Each agent handles a specific category of map elements.
    /// </summary>
    public class AgentOrchestrator
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const string API_URL = "https://openrouter.ai/api/v1/chat/completions";

        private readonly string apiKey;
        private readonly string orchestratorModel;
        private readonly string agentModel;

        // Available agent types
        public static readonly string[] AgentTypes = {
            "background",
            "platform",
            "tile",
            "life",
            "portal",
            "object",
            "settings"
        };

        /// <summary>
        /// Progress callback for UI updates
        /// </summary>
        public event Action<string> OnProgress;

        public AgentOrchestrator(string apiKey, string orchestratorModel, string agentModel = null)
        {
            this.apiKey = apiKey;
            this.orchestratorModel = orchestratorModel;
            this.agentModel = agentModel ?? orchestratorModel;
        }

        /// <summary>
        /// Process user instructions using multiple specialized agents
        /// </summary>
        public async Task<string> ProcessWithAgentsAsync(string mapContext, string userInstructions)
        {
            // Phase 1: Plan - Determine which agents are needed
            ReportProgress("Analyzing request...");
            var plan = await PlanExecutionAsync(userInstructions);

            if (plan.Agents.Count == 0)
            {
                return "# No agents selected for this request";
            }

            ReportProgress($"Plan: {plan.Reasoning}");
            ReportProgress($"Agents to run: {string.Join(", ", plan.Agents.Select(a => a.Agent))}");

            // Phase 2: Execute agents in priority order
            var allCommands = new List<string>();
            var groupedByPriority = plan.Agents.GroupBy(a => a.Priority).OrderBy(g => g.Key);

            foreach (var priorityGroup in groupedByPriority)
            {
                ReportProgress($"Executing priority {priorityGroup.Key} agents...");

                // Run agents with same priority in parallel
                var tasks = priorityGroup.Select(agentTask =>
                    ExecuteAgentAsync(agentTask.Agent, agentTask.Task, mapContext));

                var results = await Task.WhenAll(tasks);

                foreach (var result in results)
                {
                    if (result.Success)
                    {
                        allCommands.AddRange(result.Commands);
                        ReportProgress($"  {result.AgentType}: {result.Commands.Count} command(s)");
                    }
                    else
                    {
                        allCommands.Add($"# {result.AgentType} agent error: {result.Error}");
                        ReportProgress($"  {result.AgentType}: Error - {result.Error}");
                    }
                }
            }

            if (allCommands.Count == 0)
            {
                return "# No commands generated";
            }

            return string.Join(Environment.NewLine, allCommands);
        }

        /// <summary>
        /// Process user instructions using conversation history for multi-turn chat support.
        /// This method considers previous messages in the conversation to provide context-aware responses.
        /// </summary>
        /// <param name="mapContext">Current map state in AI-readable format</param>
        /// <param name="conversationHistory">Previous conversation messages (JArray)</param>
        /// <param name="latestUserMessage">The most recent user message to process</param>
        /// <returns>Combined explanation text and commands</returns>
        public async Task<string> ProcessWithConversationAsync(string mapContext, JArray conversationHistory, string latestUserMessage)
        {
            // Build context that includes conversation history
            var contextWithHistory = BuildContextWithHistory(mapContext, conversationHistory, latestUserMessage);

            // Phase 1: Plan - Determine which agents are needed based on latest request
            ReportProgress("Analyzing request...");
            var plan = await PlanExecutionAsync(latestUserMessage);

            if (plan.Agents.Count == 0)
            {
                return "I understand. Let me know if you'd like to make any changes to the map.";
            }

            ReportProgress($"Plan: {plan.Reasoning}");
            ReportProgress($"Agents to run: {string.Join(", ", plan.Agents.Select(a => a.Agent))}");

            // Phase 2: Execute agents in priority order
            var allCommands = new List<string>();
            var groupedByPriority = plan.Agents.GroupBy(a => a.Priority).OrderBy(g => g.Key);

            foreach (var priorityGroup in groupedByPriority)
            {
                ReportProgress($"Executing priority {priorityGroup.Key} agents...");

                // Run agents with same priority in parallel
                var tasks = priorityGroup.Select(agentTask =>
                    ExecuteAgentAsync(agentTask.Agent, agentTask.Task, mapContext));

                var results = await Task.WhenAll(tasks);

                foreach (var result in results)
                {
                    if (result.Success)
                    {
                        allCommands.AddRange(result.Commands);
                        ReportProgress($"  {result.AgentType}: {result.Commands.Count} command(s)");
                    }
                    else
                    {
                        allCommands.Add($"# {result.AgentType} agent error: {result.Error}");
                        ReportProgress($"  {result.AgentType}: Error - {result.Error}");
                    }
                }
            }

            // Build response with explanation
            var response = new StringBuilder();
            response.AppendLine($"I'll help you with that. {plan.Reasoning}");
            response.AppendLine();

            if (allCommands.Count > 0)
            {
                response.AppendLine(string.Join(Environment.NewLine, allCommands));
            }
            else
            {
                response.AppendLine("# No commands generated");
            }

            return response.ToString();
        }

        /// <summary>
        /// Build context string that includes relevant conversation history
        /// </summary>
        private string BuildContextWithHistory(string mapContext, JArray conversationHistory, string latestMessage)
        {
            var context = new StringBuilder();
            context.AppendLine("## Previous Conversation Context");

            // Include relevant previous exchanges (limit to last 3 for context)
            int exchangeCount = 0;
            foreach (var msg in conversationHistory)
            {
                var role = msg["role"]?.ToString();
                var content = msg["content"]?.ToString();

                if (role == "user" || role == "assistant")
                {
                    // Truncate long messages
                    if (content?.Length > 500)
                    {
                        content = content.Substring(0, 500) + "...";
                    }

                    context.AppendLine($"[{role}]: {content}");
                    context.AppendLine();

                    if (role == "assistant")
                    {
                        exchangeCount++;
                        if (exchangeCount >= 3) break;
                    }
                }
            }

            context.AppendLine("## Current Map State");
            context.AppendLine(mapContext);
            context.AppendLine();
            context.AppendLine("## Current Request");
            context.AppendLine(latestMessage);

            return context.ToString();
        }

        /// <summary>
        /// Use the orchestrator to plan which agents should run
        /// </summary>
        private async Task<OrchestrationPlan> PlanExecutionAsync(string userInstructions)
        {
            var orchestratorPrompt = LoadPrompt("OrchestratorPrompt.txt");

            var messages = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = orchestratorPrompt },
                new JObject { ["role"] = "user", ["content"] = userInstructions }
            };

            var requestBody = new JObject
            {
                ["model"] = orchestratorModel,
                ["messages"] = messages,
                ["temperature"] = 0.3, // Lower temperature for more consistent planning
                ["max_tokens"] = 2000
            };

            try
            {
                var response = await SendRequestAsync(requestBody);
                var content = response["choices"]?[0]?["message"]?["content"]?.ToString();

                if (string.IsNullOrEmpty(content))
                {
                    return new OrchestrationPlan { Reasoning = "No response from orchestrator" };
                }

                // Parse JSON from response (handle markdown code blocks)
                content = ExtractJson(content);
                var planJson = JObject.Parse(content);

                var plan = new OrchestrationPlan
                {
                    Reasoning = planJson["reasoning"]?.ToString() ?? ""
                };

                var agentsArray = planJson["agents"] as JArray;
                if (agentsArray != null)
                {
                    foreach (var agentObj in agentsArray)
                    {
                        plan.Agents.Add(new AgentTask
                        {
                            Agent = agentObj["agent"]?.ToString(),
                            Task = agentObj["task"]?.ToString(),
                            Priority = agentObj["priority"]?.Value<int>() ?? 1
                        });
                    }
                }

                return plan;
            }
            catch (Exception ex)
            {
                return new OrchestrationPlan { Reasoning = $"Planning error: {ex.Message}" };
            }
        }

        /// <summary>
        /// Execute a single specialized agent
        /// </summary>
        private async Task<AgentResult> ExecuteAgentAsync(string agentType, string task, string mapContext)
        {
            var result = new AgentResult
            {
                AgentType = agentType,
                Task = task
            };

            try
            {
                var agentPrompt = LoadAgentPrompt(agentType);
                var userMessage = BuildAgentUserMessage(agentType, mapContext, task);

                var messages = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = agentPrompt },
                    new JObject { ["role"] = "user", ["content"] = userMessage }
                };

                // Use the existing OpenRouterClient logic for function calling
                var client = new OpenRouterClient(apiKey, agentModel);
                var commands = await client.ProcessInstructionsAsync(
                    FilterMapContextForAgent(agentType, mapContext),
                    task);

                result.Commands = commands.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToList();
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Load an agent-specific prompt file with placeholder replacement
        /// </summary>
        private string LoadAgentPrompt(string agentType)
        {
            return MapEditorPromptBuilder.LoadAgentPrompt(agentType);
        }

        /// <summary>
        /// Load a prompt file from the Prompts directory with placeholder replacement
        /// </summary>
        private string LoadPrompt(string fileName)
        {
            return MapEditorPromptBuilder.LoadPromptFile(fileName);
        }

        /// <summary>
        /// Build the user message for a specific agent
        /// </summary>
        private string BuildAgentUserMessage(string agentType, string mapContext, string task)
        {
            var filteredContext = FilterMapContextForAgent(agentType, mapContext);

            return $@"## Current Map State
{filteredContext}

## Your Task
{task}

Execute this task using the available functions.";
        }

        /// <summary>
        /// Filter map context to include only relevant information for the agent type
        /// </summary>
        private string FilterMapContextForAgent(string agentType, string fullContext)
        {
            var lines = fullContext.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new StringBuilder();
            var currentSection = "";
            var includeSection = false;

            // Sections each agent needs
            var agentSections = new Dictionary<string, HashSet<string>>
            {
                ["background"] = new HashSet<string> {
                    "# Map Summary", "Map:", "Size:", "Content Bounds",
                    "## Element Counts", "## Backgrounds", "## Available Background Sets"
                },
                ["platform"] = new HashSet<string> {
                    "# Map Summary", "Map:", "Size:", "Content Bounds", "## ASCII Map",
                    "## Element Counts", "## Platforms", "## Ropes", "## Available Tilesets"
                },
                ["tile"] = new HashSet<string> {
                    "# Map Summary", "Map:", "Size:", "Content Bounds",
                    "## Platforms", "## Tiles", "## Available Tilesets"
                },
                ["life"] = new HashSet<string> {
                    "# Map Summary", "Map:", "Size:", "Content Bounds", "## ASCII Map",
                    "## Element Counts", "## Mobs", "## NPCs", "## Platforms"
                },
                ["portal"] = new HashSet<string> {
                    "# Map Summary", "Map:", "Size:", "Content Bounds",
                    "## Portals", "## Platforms"
                },
                ["object"] = new HashSet<string> {
                    "# Map Summary", "Map:", "Size:", "Content Bounds",
                    "## Objects", "## Platforms", "## Available Object Sets"
                },
                ["settings"] = new HashSet<string> {
                    "# Map Summary", "Map:", "Size:"
                }
            };

            var sectionsToInclude = agentSections.ContainsKey(agentType)
                ? agentSections[agentType]
                : new HashSet<string>();

            foreach (var line in lines)
            {
                // Check if this is a section header
                if (line.StartsWith("# ") || line.StartsWith("## ") || line.StartsWith("### "))
                {
                    currentSection = line;
                    includeSection = sectionsToInclude.Any(s => line.Contains(s) || s.Contains(line.Trim('#').Trim()));
                }

                // Always include basic map info lines
                if (line.StartsWith("Map:") || line.StartsWith("Size:") || line.StartsWith("Content Bounds") ||
                    line.Contains("IMPORTANT"))
                {
                    result.AppendLine(line);
                }
                else if (includeSection)
                {
                    result.AppendLine(line);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Extract JSON from response that might be wrapped in markdown code blocks
        /// </summary>
        private string ExtractJson(string content)
        {
            // Remove markdown code blocks if present
            if (content.Contains("```json"))
            {
                var start = content.IndexOf("```json") + 7;
                var end = content.LastIndexOf("```");
                if (end > start)
                {
                    content = content.Substring(start, end - start);
                }
            }
            else if (content.Contains("```"))
            {
                var start = content.IndexOf("```") + 3;
                var end = content.LastIndexOf("```");
                if (end > start)
                {
                    content = content.Substring(start, end - start);
                }
            }
            return content.Trim();
        }

        /// <summary>
        /// Send a request to the OpenRouter API
        /// </summary>
        private async Task<JObject> SendRequestAsync(JObject requestBody)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, API_URL);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = new StringContent(
                requestBody.ToString(Formatting.None),
                Encoding.UTF8,
                "application/json");

            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"API error: {response.StatusCode} - {responseContent}");
            }

            return JObject.Parse(responseContent);
        }

        private void ReportProgress(string message)
        {
            OnProgress?.Invoke(message);
        }
    }
}
