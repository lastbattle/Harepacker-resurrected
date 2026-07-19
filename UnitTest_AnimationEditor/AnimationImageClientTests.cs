using HaCreator.GUI.FrameAnimation.AI;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace UnitTest_AnimationEditor;

public class AnimationImageClientTests
{
    [Fact]
    public async Task GenerateSendsCompatibleJsonAndDecodesBase64()
    {
        byte[] expected = { 1, 2, 3, 4 };
        string? requestBody = null;
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://example.test/v1/images/generations", request.RequestUri?.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("secret", request.Headers.Authorization?.Parameter);
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(new JObject
            {
                ["data"] = new JArray(new JObject
                {
                    ["b64_json"] = Convert.ToBase64String(expected),
                    ["revised_prompt"] = "clean sprite"
                })
            });
        });

        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);
        GeneratedAnimationImage result = await client.GenerateAsync(new AnimationImageGenerationRequest
        {
            Prompt = "Maple monster",
            Size = "512x512"
        });

        Assert.Equal(expected, result.Data);
        Assert.Equal("clean sprite", result.RevisedPrompt);
        JObject payload = JObject.Parse(requestBody!);
        Assert.Equal("gpt-image-2", payload["model"]?.ToString());
        Assert.Equal("Maple monster", payload["prompt"]?.ToString());
        Assert.Equal("512x512", payload["size"]?.ToString());
        Assert.Null(payload["background"]);
        Assert.Equal("b64_json", payload["response_format"]?.ToString());
    }

    [Fact]
    public async Task CompatibleModelCanRequestTransparentBackgroundExplicitly()
    {
        string? requestBody = null;
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(new JObject
            {
                ["data"] = new JArray(new JObject { ["b64_json"] = "AQ==" })
            });
        });
        using var httpClient = new HttpClient(handler);
        using var client = new OpenAICompatibleImageClient(new OpenAICompatibleImageOptions
        {
            BaseUrl = "https://example.test/v1",
            Model = "gpt-image-1.5"
        }, httpClient);

        await client.GenerateAsync(new AnimationImageGenerationRequest
        {
            Prompt = "sprite",
            Background = "transparent"
        });

        Assert.Equal("transparent", JObject.Parse(requestBody!)["background"]?.ToString());
    }

    [Fact]
    public async Task EditSendsMultipartImageAndMaskAndDownloadsUrlResponse()
    {
        byte[] downloaded = { 9, 8, 7 };
        int calls = 0;
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            calls++;
            if (request.Method == HttpMethod.Get)
            {
                Assert.Equal("https://cdn.example.test/result.png", request.RequestUri?.ToString());
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(downloaded)
                };
            }

            Assert.Equal("https://example.test/v1/images/edits", request.RequestUri?.ToString());
            MultipartFormDataContent multipartContent = Assert.IsType<MultipartFormDataContent>(request.Content);
            var parts = multipartContent.ToList();
            HttpContent imagePart = Assert.Single(parts, part =>
                part.Headers.ContentDisposition?.Name?.Trim('"') == "image");
            HttpContent maskPart = Assert.Single(parts, part =>
                part.Headers.ContentDisposition?.Name?.Trim('"') == "mask");
            Assert.Equal("frame.png", imagePart.Headers.ContentDisposition?.FileName?.Trim('"'));
            Assert.Equal("mask.png", maskPart.Headers.ContentDisposition?.FileName?.Trim('"'));
            string multipart = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("paint wings", multipart);
            return JsonResponse(new JObject
            {
                ["data"] = new JArray(new JObject
                {
                    ["url"] = "https://cdn.example.test/result.png"
                })
            });
        });

        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);
        GeneratedAnimationImage result = await client.EditAsync(new AnimationImageEditRequest
        {
            Prompt = "paint wings",
            Image = new byte[] { 1, 2 },
            Mask = new byte[] { 3, 4 }
        });

        Assert.Equal(2, calls);
        Assert.Equal(downloaded, result.Data);
        Assert.Equal("https://cdn.example.test/result.png", result.SourceUri?.ToString());
    }

    [Fact]
    public async Task ProviderErrorExposesStatusBodyAndMessage()
    {
        const string body = "{\"error\":{\"message\":\"model unavailable\"}}";
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            ReasonPhrase = "Bad Request",
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        }));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        OpenAICompatibleImageApiException exception = await Assert.ThrowsAsync<OpenAICompatibleImageApiException>(() =>
            client.GenerateAsync(new AnimationImageGenerationRequest { Prompt = "sprite" }));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(body, exception.ResponseBody);
        Assert.Contains("model unavailable", exception.Message);
    }

    [Fact]
    public async Task CancellationIsPropagatedWithoutApiWrapping()
    {
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        });
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GenerateAsync(
            new AnimationImageGenerationRequest { Prompt = "sprite" }, cancellation.Token));
    }

    [Fact]
    public async Task DataUrlResponseIsDecodedWithoutNetworkDownload()
    {
        byte[] expected = { 5, 4, 3 };
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(new JObject
        {
            ["data"] = new JArray(new JObject
            {
                ["url"] = "data:image/png;base64," + Convert.ToBase64String(expected)
            })
        })));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        GeneratedAnimationImage result = await client.GenerateAsync(
            new AnimationImageGenerationRequest { Prompt = "sprite" });

        Assert.Equal(expected, result.Data);
    }

    [Fact]
    public async Task ServiceDecodesAndLocallyCleansGeneratedFrame()
    {
        byte[] png;
        using (var bitmap = new Bitmap(6, 6, PixelFormat.Format32bppArgb))
        {
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
                graphics.FillRectangle(Brushes.Black, 2, 1, 2, 4);
            }
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            png = stream.ToArray();
        }

        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(new JObject
        {
            ["data"] = new JArray(new JObject { ["b64_json"] = Convert.ToBase64String(png) })
        })));
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);
        using var service = new AnimationImageService(client);

        using AnimationImageResult result = await service.GenerateAsync(
            new AnimationImageGenerationRequest { Prompt = "sprite" });

        Assert.Equal(2, result.Bitmap.Width);
        Assert.Equal(4, result.Bitmap.Height);
        Assert.Equal(new Point(1, 3), result.Origin);
        Assert.Equal(255, result.Bitmap.GetPixel(0, 0).A);
    }

    private static OpenAICompatibleImageClient CreateClient(HttpClient httpClient) => new(
        new OpenAICompatibleImageOptions
        {
            BaseUrl = "https://example.test/v1",
            ApiKey = "secret",
            Model = "gpt-image-2"
        }, httpClient);

    private static HttpResponseMessage JsonResponse(JObject body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;
        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
            this.handler = handler;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request, cancellationToken);
    }
}
