using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HaCreator.MapEditor.AI;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class OpenCodeLiveTests
    {
        [Fact]
        public async Task Live_OpenCodeAutoStart_SyncsTools_ForCurrentDirectory()
        {
            if (!IsLiveTestEnabled())
            {
                return;
            }

            var model = Environment.GetEnvironmentVariable("OPENCODE_LIVE_MODEL");
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "openai/gpt-5.3-codex";
            }

            var client = new OpenCodeClient(
                host: "127.0.0.1",
                port: 4096,
                model: model,
                autoStart: true,
                reasoningEffort: "medium");

            try
            {
                await client.EnsureServerAsync();
                Assert.True(await client.IsServerRunningAsync(), "OpenCode server did not become ready.");

                var toolDir = Path.Combine(client.ProjectDirectory, ".opencode", "tool");
                Assert.True(Directory.Exists(toolDir), $"Tool directory missing: {toolDir}");

                var requiredToolNames = MapEditorFunctions.GetToolDefinitions()
                    .OfType<JObject>()
                    .Select(tool => tool["function"]?["name"]?.ToString())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct()
                    .ToList();

                Assert.NotEmpty(requiredToolNames);

                foreach (var toolName in requiredToolNames)
                {
                    var wrapperPath = Path.Combine(toolDir, $"{toolName}.ts");
                    Assert.True(File.Exists(wrapperPath), $"Missing generated tool wrapper: {wrapperPath}");
                }

                var registeredToolIds = await client.GetRegisteredToolIdsForCurrentDirectoryAsync(ensureServer: false);
                if (registeredToolIds != null && registeredToolIds.Count > 0)
                {
                    foreach (var toolName in requiredToolNames)
                    {
                        Assert.Contains(toolName, registeredToolIds);
                    }
                }
            }
            finally
            {
                OpenCodeClient.StopServer();
            }
        }

        [Fact]
        public async Task Live_OpenCodeRunWithTools_InvokesRegisteredProjectTool()
        {
            if (!IsLiveTestEnabled())
            {
                return;
            }

            var model = Environment.GetEnvironmentVariable("OPENCODE_LIVE_MODEL");
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "openai/gpt-5.3-codex";
            }

            var client = new OpenCodeClient(
                host: "127.0.0.1",
                port: 4096,
                model: model,
                autoStart: true,
                reasoningEffort: "medium");

            try
            {
                await client.EnsureServerAsync();
                OpenCodeClient.ClearCollectedCommands();

                var result = await client.RunWithToolsAsync(
                    text: "Use remove_element exactly once with element_type='mob' x=10 y=20. After the tool returns, reply with done.",
                    tools: MapEditorFunctions.GetToolDefinitions(),
                    toolExecutor: (name, args) =>
                    {
                        return new JObject
                        {
                            ["unexpected_local_tool_executor"] = name
                        };
                    },
                    systemPrompt: "You are running a tool invocation smoke test. You must use the provided tool when the user explicitly asks for a specific map edit action.",
                    sessionTitle: "tool-smoke-test",
                    maxIterations: 4,
                    reasoningEffort: "low");

                Assert.True(result.Success, result.Error ?? "OpenCode RunWithToolsAsync failed.");
                Assert.NotNull(result.ToolCallsMade);
                Assert.NotEmpty(result.ToolCallsMade);

                var toolCall = Assert.Single(result.ToolCallsMade.Where(call => call.Name == "remove_element"));
                Assert.False(toolCall.IsError);
                Assert.Equal("mob", toolCall.Arguments["element_type"]?.ToString());
                Assert.Equal("10", toolCall.Arguments["x"]?.ToString());
                Assert.Equal("20", toolCall.Arguments["y"]?.ToString());
                Assert.Contains("DELETE MOB at (10, 20)", OpenCodeClient.GetCollectedCommands());
            }
            finally
            {
                OpenCodeClient.StopServer();
            }
        }

        [Fact]
        public async Task Live_OpenCodeProcessInstructions_ReturnsServerSideCommand()
        {
            if (!IsLiveTestEnabled())
            {
                return;
            }

            var model = Environment.GetEnvironmentVariable("OPENCODE_LIVE_MODEL");
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "openai/gpt-5.3-codex";
            }

            var client = new OpenCodeClient(
                host: "127.0.0.1",
                port: 4096,
                model: model,
                autoStart: true,
                reasoningEffort: "medium");

            try
            {
                await client.EnsureServerAsync();

                var mapContext =
                    "# Map Summary" + Environment.NewLine +
                    "Map: TestMap" + Environment.NewLine +
                    "Content Bounds: X=[0 to 1000], Y=[0 to 1000]" + Environment.NewLine +
                    "## Mobs" + Environment.NewLine +
                    "- Mob at x=10, y=20";

                var result = await client.ProcessInstructionsAsync(
                    mapContext,
                    "Remove the mob at x 10 y 20.");

                Assert.Contains("DELETE MOB at (10, 20)", result);
            }
            finally
            {
                OpenCodeClient.StopServer();
            }
        }

        private static bool IsLiveTestEnabled()
        {
            var value = Environment.GetEnvironmentVariable("RUN_OPENCODE_LIVE_TEST");
            if (string.IsNullOrWhiteSpace(value))
            {
                value = Environment.GetEnvironmentVariable("OPENCODE_LIVE_TEST");
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim().ToLowerInvariant();
            return normalized == "1" ||
                   normalized == "true" ||
                   normalized == "yes" ||
                   normalized == "y" ||
                   normalized == "on";
        }
    }
}
