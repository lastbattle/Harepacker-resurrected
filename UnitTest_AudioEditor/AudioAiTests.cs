using HaSharedLibrary.Audio.AI;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace UnitTest_AudioEditor;

public class AudioAiTests
{
    [Fact]
    public void PromptCompiler_DefaultsBgmToInstrumentalAndLoop()
    {
        var brief = new AudioAiPromptCompiler().Compile("gentle forest town theme", "Town", "Maple forest", true, 30);
        Assert.True(brief.Instrumental);
        Assert.True(brief.LoopIntent);
        Assert.Contains("Maple forest", brief.Prompt);
        string prompt = new AudioAiPromptCompiler().CompileProviderPrompt(brief, out var warnings);
        Assert.Contains("instrumental", prompt);
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task FakeProvider_StreamsCandidateAndSupportsSelection()
    {
        string file = Path.GetTempFileName();
        try
        {
            var provider = new FakeAudioAiProvider(file);
            var registry = new AudioAiProviderRegistry(); registry.Register(provider);
            var selected = await registry.SelectAsync(AudioAiCapability.TextToMusic, true, null, CancellationToken.None);
            var job = await selected.StartAsync(new AudioAiRequest { Brief = new AudioAiBrief { Prompt = "boss battle", DurationSeconds = 30 } }, CancellationToken.None);
            var events = new List<AudioAiJobEvent>(); await foreach (var item in selected.WatchAsync(job, CancellationToken.None)) events.Add(item);
            Assert.Contains(events, item => item.Kind == AudioAiJobEventKind.Candidate);
            Assert.Equal(AudioAiJobEventKind.Completed, events[^1].Kind);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void UploadAuthorization_IsProviderArtifactAndByteScoped()
    {
        var input = new AudioAiInputArtifact { ArtifactId = "a", ByteLength = 10 };
        var authorization = new UploadAuthorization { ProviderId = "cloud", ArtifactIds = { "a" }, AuthorizedBytes = 10, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1) };
        Assert.True(authorization.Matches("cloud", new[] { input }));
        Assert.False(authorization.Matches("other", new[] { input }));
    }

    [Fact]
    public async Task JobStore_RoundTripsWithoutSecretValues()
    {
        string root = Path.Combine(Path.GetTempPath(), "ha-audio-ai-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new AudioAiJobStore(root); var handle = new AudioAiJobHandle(Guid.NewGuid(), "remote", true);
            await store.CreateAsync(handle, new AudioAiRequest { Brief = new AudioAiBrief { Prompt = "town" } });
            await store.WriteStateAsync(handle.LocalJobId, new AudioAiPersistedState { State = AudioAiJobState.Completed, Progress = 1 });
            var state = await store.ReadStateAsync(handle.LocalJobId);
            Assert.Equal(AudioAiJobState.Completed, state!.State);
            Assert.DoesNotContain("api-key", File.ReadAllText(Path.Combine(store.GetJobDirectory(handle.LocalJobId), "request.json")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Brief_RejectsUnknownSchema()
    {
        var brief = new AudioAiBrief { SchemaVersion = 99, Prompt = "town" };
        Assert.Throws<InvalidDataException>(brief.Validate);
    }

    [Fact]
    public async Task AceStepAdapter_RunsAgainstLiveLoopbackSidecar()
    {
        int port; var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Task server = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                HttpListenerContext context;
                try { context = await listener.GetContextAsync().WaitAsync(stop.Token); } catch { break; }
                string body = context.Request.Url!.AbsolutePath switch
                {
                    "/health" => "{\"data\":{\"version\":\"test-live\"}}",
                    "/v1/models" => "{\"data\":[]}",
                    "/release_task" => "{\"data\":{\"task_id\":\"live-1\"}}",
                    "/query_result" => "{\"data\":[{\"status\":1,\"result\":\"[{\\\"file\\\":\\\"/v1/audio?path=C%3A%5CTemp%5Ccandidate.wav\\\"}]\"}]}",
                    _ => "{}"
                };
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(body); context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes); context.Response.Close();
            }
        }, stop.Token);
        using var provider = new AceStepLocalAudioAiProvider($"http://127.0.0.1:{port}");
        var info = await provider.GetInfoAsync(stop.Token); Assert.True(info.Healthy); Assert.Equal("test-live", info.Version);
        var job = await provider.StartAsync(new AudioAiRequest { Brief = new AudioAiBrief { Prompt = "live town loop" } }, stop.Token);
        AudioAiJobEvent? last = null; AudioAiArtifact? candidate = null; await foreach (var item in provider.WatchAsync(job, stop.Token)) { last = item; candidate ??= item.Artifact; }
        Assert.Equal(@"C:\Temp\candidate.wav", candidate!.LocalPath);
        Assert.Equal(AudioAiJobEventKind.Completed, last!.Kind);
        stop.Cancel(); listener.Stop(); await server;
    }

    [Fact]
    public async Task AceStepAdapter_ReportsEmptyCompletedArtifactAsFailure()
    {
        using var http = new HttpClient(new StaticResponseHandler(
            "{\"data\":[{\"status\":1,\"progress_text\":\"ffmpeg executable not found\",\"result\":\"[{\\\"file\\\":\\\"\\\"}]\"}]}"));
        using var provider = new AceStepLocalAudioAiProvider("http://127.0.0.1:1", httpClient: http);
        var events = new List<AudioAiJobEvent>();
        await foreach (AudioAiJobEvent item in provider.WatchAsync(
            new AudioAiJobHandle(Guid.NewGuid(), "failed-mp3"), CancellationToken.None))
            events.Add(item);

        AudioAiJobEvent failure = Assert.Single(events);
        Assert.Equal(AudioAiJobEventKind.Failed, failure.Kind);
        Assert.Contains("ffmpeg", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StaticResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
    }
}
