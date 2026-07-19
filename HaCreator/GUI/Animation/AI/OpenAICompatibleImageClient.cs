#nullable enable

using HaCreator.MapEditor.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.GUI.FrameAnimation.AI
{
    public sealed class OpenAICompatibleImageOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-image-2";
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

        public static OpenAICompatibleImageOptions FromSettings()
        {
            return new OpenAICompatibleImageOptions
            {
                BaseUrl = AISettings.BaseUrl,
                ApiKey = AISettings.ApiKey,
                Model = AISettings.ImageModel
            };
        }
    }

    public class AnimationImageGenerationRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public string Size { get; set; } = "1024x1024";
        public string Quality { get; set; } = "high";
        // gpt-image-2 does not accept background=transparent. Leave this empty by default and
        // use the local edge-matte cleanup; compatible models may opt in explicitly.
        public string Background { get; set; } = string.Empty;
        public string OutputFormat { get; set; } = "png";
    }

    public sealed class AnimationImageEditRequest : AnimationImageGenerationRequest
    {
        public byte[] Image { get; set; } = Array.Empty<byte>();
        public string ImageFileName { get; set; } = "frame.png";
        public byte[]? Mask { get; set; }
        public string MaskFileName { get; set; } = "mask.png";
    }

    public sealed class GeneratedAnimationImage
    {
        public GeneratedAnimationImage(byte[] data, string revisedPrompt, Uri? sourceUri)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            RevisedPrompt = revisedPrompt ?? string.Empty;
            SourceUri = sourceUri;
        }

        public byte[] Data { get; }
        public string RevisedPrompt { get; }
        public Uri? SourceUri { get; }
    }

    public sealed class OpenAICompatibleImageApiException : Exception
    {
        public OpenAICompatibleImageApiException(string message, HttpStatusCode? statusCode = null,
            string? responseBody = null, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }

        public HttpStatusCode? StatusCode { get; }
        public string? ResponseBody { get; }
    }

    /// <summary>
    /// Small OpenAI-compatible client for /images/generations and /images/edits.
    /// HttpClient injection keeps the transport deterministic and offline-testable.
    /// </summary>
    public sealed class OpenAICompatibleImageClient : IDisposable
    {
        private readonly OpenAICompatibleImageOptions options;
        private readonly HttpClient httpClient;
        private readonly bool ownsHttpClient;

        public OpenAICompatibleImageClient(OpenAICompatibleImageOptions options, HttpClient? httpClient = null)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.httpClient = httpClient ?? new HttpClient();
            ownsHttpClient = httpClient == null;
            if (ownsHttpClient)
                this.httpClient.Timeout = options.Timeout;
        }

        public Task<GeneratedAnimationImage> GenerateAsync(AnimationImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request);
            JObject body = CreateCommonBody(request);
            body["n"] = 1;
            body["response_format"] = "b64_json";

            var content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
            return SendAsync("images/generations", content, cancellationToken);
        }

        public Task<GeneratedAnimationImage> EditAsync(AnimationImageEditRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request);
            if (request.Image == null || request.Image.Length == 0)
                throw new ArgumentException("An input image is required for an image edit.", nameof(request));

            var content = new MultipartFormDataContent();
            AddString(content, "model", options.Model);
            AddString(content, "prompt", request.Prompt);
            AddOptionalString(content, "size", request.Size);
            AddOptionalString(content, "quality", request.Quality);
            AddOptionalString(content, "background", request.Background);
            AddOptionalString(content, "output_format", request.OutputFormat);
            AddString(content, "n", "1");
            AddString(content, "response_format", "b64_json");
            content.Add(CreatePngContent(request.Image), "image", SanitizeFileName(request.ImageFileName, "frame.png"));
            if (request.Mask?.Length > 0)
                content.Add(CreatePngContent(request.Mask), "mask", SanitizeFileName(request.MaskFileName, "mask.png"));

            return SendAsync("images/edits", content, cancellationToken);
        }

        private async Task<GeneratedAnimationImage> SendAsync(string relativePath, HttpContent content,
            CancellationToken cancellationToken)
        {
            ValidateOptions();
            using (content)
            using (var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(relativePath)))
            {
                request.Content = content;
                ApplyHeaders(request);
                using (HttpResponseMessage response = await httpClient.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        throw CreateApiException(response.StatusCode, response.ReasonPhrase, responseBody);

                    return await ParseResponseAsync(responseBody, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task<GeneratedAnimationImage> ParseResponseAsync(string responseBody,
            CancellationToken cancellationToken)
        {
            JObject payload;
            try
            {
                payload = JObject.Parse(responseBody);
            }
            catch (JsonException ex)
            {
                throw new OpenAICompatibleImageApiException(
                    $"The image endpoint returned invalid JSON: {ex.Message}", responseBody: responseBody,
                    innerException: ex);
            }

            JToken? item = (payload["data"] as JArray)?.FirstOrDefault();
            if (item == null)
                throw new OpenAICompatibleImageApiException(
                    "The image endpoint response did not contain an image.", responseBody: responseBody);

            string revisedPrompt = item["revised_prompt"]?.ToString() ?? string.Empty;
            string? base64 = item["b64_json"]?.ToString();
            if (!string.IsNullOrWhiteSpace(base64))
            {
                try
                {
                    return new GeneratedAnimationImage(Convert.FromBase64String(base64), revisedPrompt, null);
                }
                catch (FormatException ex)
                {
                    throw new OpenAICompatibleImageApiException(
                        "The image endpoint returned invalid base64 image data.", responseBody: responseBody,
                        innerException: ex);
                }
            }

            string? urlText = item["url"]?.ToString();
            if (!Uri.TryCreate(urlText, UriKind.Absolute, out Uri? imageUri))
                throw new OpenAICompatibleImageApiException(
                    "The image endpoint response contained neither b64_json nor a valid URL.", responseBody: responseBody);

            if (imageUri.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase))
                return new GeneratedAnimationImage(ParseDataUri(imageUri, responseBody), revisedPrompt, imageUri);
            if (imageUri.Scheme != Uri.UriSchemeHttp && imageUri.Scheme != Uri.UriSchemeHttps)
                throw new OpenAICompatibleImageApiException(
                    $"Unsupported image URL scheme: {imageUri.Scheme}.", responseBody: responseBody);

            using (var response = await httpClient.GetAsync(
                imageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                byte[] data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw CreateApiException(response.StatusCode, response.ReasonPhrase,
                        Encoding.UTF8.GetString(data));
                if (data.Length == 0)
                    throw new OpenAICompatibleImageApiException("The image URL returned an empty response.");
                return new GeneratedAnimationImage(data, revisedPrompt, imageUri);
            }
        }

        private JObject CreateCommonBody(AnimationImageGenerationRequest request)
        {
            var body = new JObject
            {
                ["model"] = options.Model,
                ["prompt"] = request.Prompt
            };
            AddOptional(body, "size", request.Size);
            AddOptional(body, "quality", request.Quality);
            AddOptional(body, "background", request.Background);
            AddOptional(body, "output_format", request.OutputFormat);
            return body;
        }

        private void ApplyHeaders(HttpRequestMessage request)
        {
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey.Trim());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private Uri BuildEndpoint(string relativePath)
        {
            string root = options.BaseUrl.Trim().TrimEnd('/') + "/";
            return new Uri(new Uri(root, UriKind.Absolute), relativePath);
        }

        private void ValidateOptions()
        {
            if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? baseUri) || baseUri == null ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("A valid HTTP(S) AI base URL is required.");
            if (string.IsNullOrWhiteSpace(options.Model))
                throw new InvalidOperationException("An image model is required.");
        }

        private static void ValidateRequest(AnimationImageGenerationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Prompt))
                throw new ArgumentException("An image prompt is required.", nameof(request));
        }

        private static OpenAICompatibleImageApiException CreateApiException(
            HttpStatusCode statusCode, string? reasonPhrase, string responseBody)
        {
            string? providerMessage = null;
            try { providerMessage = JObject.Parse(responseBody)["error"]?["message"]?.ToString(); }
            catch (JsonException) { }
            string message = string.IsNullOrWhiteSpace(providerMessage)
                ? $"Image API error: {(int)statusCode} {reasonPhrase}."
                : $"Image API error: {providerMessage}";
            return new OpenAICompatibleImageApiException(message, statusCode, responseBody);
        }

        private static byte[] ParseDataUri(Uri uri, string responseBody)
        {
            string value = uri.OriginalString;
            int comma = value.IndexOf(',');
            if (comma < 0 || value[..comma].IndexOf(";base64", StringComparison.OrdinalIgnoreCase) < 0)
                throw new OpenAICompatibleImageApiException(
                    "The image endpoint returned an unsupported data URL.", responseBody: responseBody);
            try { return Convert.FromBase64String(value[(comma + 1)..]); }
            catch (FormatException ex)
            {
                throw new OpenAICompatibleImageApiException(
                    "The image endpoint returned an invalid base64 data URL.", responseBody: responseBody,
                    innerException: ex);
            }
        }

        private static ByteArrayContent CreatePngContent(byte[] data)
        {
            var content = new ByteArrayContent(data);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return content;
        }

        private static string SanitizeFileName(string? value, string fallback)
        {
            string fileName = Path.GetFileName(string.IsNullOrWhiteSpace(value) ? fallback : value);
            return string.IsNullOrWhiteSpace(fileName) ? fallback : fileName;
        }

        private static void AddOptional(JObject body, string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                body[name] = value.Trim();
        }

        private static void AddString(MultipartFormDataContent content, string name, string value) =>
            content.Add(new StringContent(value ?? string.Empty, Encoding.UTF8), name);

        private static void AddOptionalString(MultipartFormDataContent content, string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                AddString(content, name, value.Trim());
        }

        public void Dispose()
        {
            if (ownsHttpClient)
                httpClient.Dispose();
        }
    }
}
