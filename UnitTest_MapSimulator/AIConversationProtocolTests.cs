using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Text;
using HaCreator.MapEditor.AI;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UnitTest_MapSimulator;

public class AIConversationProtocolTests
{
    [Fact]
    public async Task StopPreventsRemainingMutationsInTheSameResponse()
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        using var endpoint = new HttpListener();
        endpoint.Prefixes.Add($"http://127.0.0.1:{port}/");
        endpoint.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var stop = new CancellationTokenSource();
        var serve = Task.Run(async () =>
        {
            var context = await endpoint.GetContextAsync().WaitAsync(timeout.Token);
            using var reader = new StreamReader(context.Request.InputStream);
            await reader.ReadToEndAsync(timeout.Token);
            var calls = new JArray(Enumerable.Range(0, 2).Select(i => new JObject
            {
                ["type"] = "function_call", ["call_id"] = $"chair{i}", ["name"] = "add_chair",
                ["arguments"] = new JObject { ["x"] = i * 100, ["y"] = 0 }.ToString()
            }));
            var bytes = Encoding.UTF8.GetBytes(new JObject { ["output"] = calls }.ToString());
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, timeout.Token);
            context.Response.Close();
        }, timeout.Token);
        int applied = 0;
        using var tools = new MapMcpToolServer { CommandExecutor = _ => { applied++; stop.Cancel(); return "Applied"; } };
        using var client = new OpenAICompatibleClient(new OpenAICompatibleOptions
        { BaseUrl = $"http://127.0.0.1:{port}", Model = "gpt-6-astra", Protocol = AIEndpointProtocol.Responses }, tools);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ProcessConversationAsync("map", "add chairs",
            new JArray(), null!, true, stop.Token));
        await serve;
        Assert.Equal(1, applied);
    }

    [Theory]
    [InlineData(AIEndpointProtocol.Responses)]
    [InlineData(AIEndpointProtocol.ChatCompletions)]
    public async Task VisionAndLiveToolFeedbackSurviveConversation(AIEndpointProtocol protocol)
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        using var endpoint = new HttpListener();
        endpoint.Prefixes.Add($"http://127.0.0.1:{port}/");
        endpoint.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var requests = new List<JObject>();
        var serve = Task.Run(async () =>
        {
            for (int turn = 0; turn < 2; turn++)
            {
                var context = await endpoint.GetContextAsync().WaitAsync(timeout.Token);
                using var reader = new StreamReader(context.Request.InputStream);
                requests.Add(JObject.Parse(await reader.ReadToEndAsync(timeout.Token)));
                JObject payload = protocol == AIEndpointProtocol.Responses
                    ? new JObject { ["output"] = turn == 0 ? new JArray(
                        new JObject { ["type"] = "function_call", ["call_id"] = "view", ["name"] = "get_map_view", ["arguments"] = "{}" },
                        new JObject { ["type"] = "function_call", ["call_id"] = "chair", ["name"] = "add_chair", ["arguments"] = "{\"x\":120,\"y\":80}" })
                        : new JArray(new JObject { ["type"] = "message", ["role"] = "assistant", ["content"] = new JArray(new JObject { ["type"] = "output_text", ["text"] = "Placed the chair." }) }) }
                    : new JObject { ["choices"] = new JArray(new JObject { ["message"] = turn == 0
                        ? new JObject { ["role"] = "assistant", ["tool_calls"] = new JArray(
                            ChatCall("view", "get_map_view", "{}"), ChatCall("chair", "add_chair", "{\"x\":120,\"y\":80}")) }
                        : new JObject { ["role"] = "assistant", ["content"] = "Placed the chair." } }) };
                byte[] bytes = Encoding.UTF8.GetBytes(payload.ToString());
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, timeout.Token);
                context.Response.Close();
            }
        }, timeout.Token);
        int applied = 0;
        var visual = new JArray(new JObject { ["type"] = "text", ["text"] = "worldX = 10 + pixelX" },
            new JObject { ["type"] = "image", ["mimeType"] = "image/png", ["data"] = "aW1hZ2U=" });
        using var tools = new MapMcpToolServer
        {
            RichQueryExecutor = (_, _) => visual,
            CommandExecutor = _ => { applied++; return "Applied one chair at (120,80)."; }
        };
        using var client = new OpenAICompatibleClient(new OpenAICompatibleOptions
        {
            BaseUrl = $"http://127.0.0.1:{port}", Model = "test-model", Protocol = protocol,
            ReasoningEffort = "low", MaxToolTurns = 3
        }, tools);
        var history = new JArray(new JObject { ["role"] = "user", ["content"] = "Keep the existing scenery." });
        string result = await client.ProcessConversationAsync("Map geometry", "Place a chair", history, visual, true, timeout.Token);
        await serve;
        Assert.Equal(1, applied);
        Assert.Contains("Placed the chair.", result);
        Assert.Contains("ADD CHAIR", result);
        Assert.Contains("Keep the existing scenery.", requests[0].ToString());
        Assert.Contains("data:image/png;base64,aW1hZ2U=", requests[0].ToString());
        Assert.Contains("Applied one chair", requests[1].ToString());
        Assert.Contains("worldX = 10 + pixelX", requests[1].ToString());
        Assert.Equal("low", protocol == AIEndpointProtocol.Responses
            ? requests[0]["reasoning"]?["effort"]?.ToString() : requests[0]["reasoning_effort"]?.ToString());
        if (protocol == AIEndpointProtocol.Responses)
        {
            Assert.Null(requests[0]["reasoning_effort"]);
            var output = ((JArray)requests[1]["input"]!).OfType<JObject>().Single(i => i["call_id"]?.ToString() == "view" && i["type"]?.ToString() == "function_call_output");
            Assert.Contains(((JArray)output["output"]!).OfType<JObject>(), b => b["type"]?.ToString() == "input_image");
        }
    }

    private static JObject ChatCall(string id, string name, string args) => new()
    {
        ["id"] = id, ["type"] = "function", ["function"] = new JObject { ["name"] = name, ["arguments"] = args }
    };
}
