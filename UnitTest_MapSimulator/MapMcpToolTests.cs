using System.Linq;
using HaCreator.MapEditor.AI;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class MapMcpToolTests
    {
        [Fact]
        public void McpToolList_MatchesMapEditorToolRegistry()
        {
            var expected = MapEditorFunctions.GetToolDefinitions()
                .OfType<JObject>()
                .Select(tool => tool["function"]?["name"]?.ToString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .OrderBy(name => name)
                .ToArray();

            using (var server = new MapMcpToolServer())
            {
                var actual = server.GetMcpTools()
                    .OfType<JObject>()
                    .Select(tool => tool["name"]?.ToString())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct()
                    .OrderBy(name => name)
                    .ToArray();

                Assert.Equal(expected, actual);
                Assert.Equal(34, actual.Length);
                Assert.All(server.GetMcpTools().OfType<JObject>(), tool =>
                {
                    Assert.Equal("object", tool["inputSchema"]?["type"]?.ToString());
                    Assert.NotNull(tool["description"]);
                });
            }
        }

        [Fact]
        public void McpActionTools_EnforceRequiredDiscoveryQueries()
        {
            using (var server = new MapMcpToolServer())
            {
                var result = server.CallTool("add_mob", new JObject
                {
                    ["mob_id"] = "100100",
                    ["x"] = 0,
                    ["y"] = 0
                });

                Assert.False(result.Success);
                Assert.Contains("get_mob_list", result.Text);
            }
        }

        [Fact]
        public void McpActionTools_ReturnTheSameCommandsAsOpenAiFunctionCalls()
        {
            using (var server = new MapMcpToolServer())
            {
                var result = server.CallTool("add_chair", new JObject
                {
                    ["x"] = 120,
                    ["y"] = 80
                });

                Assert.True(result.Success);
                Assert.Equal("ADD CHAIR at (120, 80)", result.Command);
            }
        }
    }
}
