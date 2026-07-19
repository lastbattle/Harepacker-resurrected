using HaCreator.GUI.FrameAnimation.AI;
using HaCreator.MapEditor.AI;
using System.Net;
using System.Net.Http;
using System.Text;

namespace UnitTest_AnimationEditor;

public class AnimationPromptSuggestionClientTests
{
    [Fact]
    public async Task ChatCompletionsReturnsPromptAndUsesBearerToken()
    {
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("secret", request.Headers.Authorization?.Parameter);
            string body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("gpt-test", body);
            return Json("{\"choices\":[{\"message\":{\"content\":\" final sprite prompt \"}}]}");
        });
        using var httpClient = new HttpClient(handler);
        using var client = new AnimationPromptSuggestionClient(new AnimationPromptSuggestionOptions
        {
            BaseUrl = "https://example.invalid/v1",
            ApiKey = "secret",
            Model = "gpt-test"
        }, httpClient);

        string result = await client.SuggestAsync("jump", "monster idle");

        Assert.Equal("final sprite prompt", result);
    }

    [Fact]
    public async Task ResponsesDialectReadsNestedOutputText()
    {
        var handler = new StubHandler((request, cancellationToken) => Task.FromResult(Json(
            "{\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"response prompt\"}]}]}")));
        using var httpClient = new HttpClient(handler);
        using var client = new AnimationPromptSuggestionClient(new AnimationPromptSuggestionOptions
        {
            BaseUrl = "https://example.invalid/v1",
            Model = "gpt-test",
            Protocol = AIEndpointProtocol.Responses
        }, httpClient);

        Assert.Equal("response prompt", await client.SuggestAsync("jump", "monster idle"));
    }

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback;
        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) =>
            this.callback = callback;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => callback(request, cancellationToken);
    }
}
