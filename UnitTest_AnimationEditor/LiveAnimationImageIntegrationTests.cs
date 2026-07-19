using HaCreator.GUI.FrameAnimation.AI;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace UnitTest_AnimationEditor;

public class LiveAnimationImageIntegrationTests
{
    [Fact]
    public async Task ConfiguredEndpointSuggestsProductionPrompt()
    {
        string? baseUrl = Environment.GetEnvironmentVariable("AI_IMAGE_LIVE_BASE_URL");
        string? apiKey = Environment.GetEnvironmentVariable("AI_IMAGE_LIVE_API_KEY");
        string? model = Environment.GetEnvironmentVariable("AI_TEXT_LIVE_MODEL");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
            return;

        using var client = new AnimationPromptSuggestionClient(new AnimationPromptSuggestionOptions
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            Model = model
        });
        string suggestion = await client.SuggestAsync(
            "make the mushroom begin a happy jump",
            "Monster; orange mushroom; idle track; 42x38 pixels; origin 21,38.");

        Assert.True(suggestion.Length >= 40);
        Assert.DoesNotContain("```", suggestion);
    }

    [Fact]
    public async Task ConfiguredEndpointGeneratesAndPostProcessesSprite()
    {
        string? baseUrl = Environment.GetEnvironmentVariable("AI_IMAGE_LIVE_BASE_URL");
        string? apiKey = Environment.GetEnvironmentVariable("AI_IMAGE_LIVE_API_KEY");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
            return;

        string model = Environment.GetEnvironmentVariable("AI_IMAGE_LIVE_MODEL") ?? "gpt-image-2";
        using var client = new OpenAICompatibleImageClient(new OpenAICompatibleImageOptions
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            Model = model,
            Timeout = TimeSpan.FromMinutes(5)
        });
        GeneratedAnimationImage generated = await client.GenerateAsync(new AnimationImageGenerationRequest
        {
            Prompt = "A single small cheerful orange mushroom monster performing one squash-and-stretch idle pose, " +
                     "2D MapleStory-style game sprite, centered with generous padding, perfectly flat solid #FF00FF background, " +
                     "no scenery, no floor, no shadow, no text, no border, no watermark.",
            Quality = "low"
        });

        Assert.NotEmpty(generated.Data);
        string? rawOutputPath = Environment.GetEnvironmentVariable("AI_IMAGE_LIVE_RAW_OUTPUT");
        if (!string.IsNullOrWhiteSpace(rawOutputPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(rawOutputPath)!);
            File.WriteAllBytes(rawOutputPath, generated.Data);
        }
        using var stream = new MemoryStream(generated.Data, writable: false);
        using var decoded = new Bitmap(stream);
        using ProcessedAnimationImage processed = AnimationImageProcessor.Process(decoded, options:
            new AnimationImageProcessingOptions
            {
                RemoveEdgeBackground = true,
                TrimTransparentPixels = true,
                TransparentPadding = 2
            });
        Assert.True(processed.Bitmap.Width > 0);
        Assert.True(processed.Bitmap.Height > 0);
        Assert.NotNull(AnimationImageProcessor.FindAlphaBounds(processed.Bitmap));
        int visibleMagentaPixels = 0;
        for (int y = 0; y < processed.Bitmap.Height; y++)
        for (int x = 0; x < processed.Bitmap.Width; x++)
        {
            Color pixel = processed.Bitmap.GetPixel(x, y);
            if (pixel.A > 32 && pixel.R > 120 && pixel.B > 100 && pixel.G < 80)
                visibleMagentaPixels++;
        }
        Assert.InRange(visibleMagentaPixels, 0, 12);

        string? outputPath = Environment.GetEnvironmentVariable("AI_IMAGE_LIVE_OUTPUT");
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            processed.Bitmap.Save(outputPath, ImageFormat.Png);
        }
    }
}
