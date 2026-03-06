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
