using HaCreator.MapEditor.AI;
using Xunit;

namespace UnitTest_MapSimulator;

public class AISettingsModelTests
{
    [Theory]
    [InlineData("https://gateway.example/v1", "openai/gpt-6-astra", "gpt-6-astra")]
    [InlineData("https://api.openai.com/v1", "openai/gpt-6-astra", "gpt-6-astra")]
    [InlineData("https://openrouter.ai/api/v1", "gpt-6-astra", "openai/gpt-6-astra")]
    [InlineData("https://openrouter.ai/api/v1", "openai/gpt-6-astra", "openai/gpt-6-astra")]
    [InlineData("https://custom.example/v1", "vendor/another-model", "vendor/another-model")]
    [InlineData("https://custom.example/v1", "openai/gpt-6-atra", "openai/gpt-6-atra")]
    public void ModelNamingMatchesEndpoint(string endpoint, string entered, string expected)
        => Assert.Equal(expected, AISettings.NormalizeModelId(endpoint, entered));

    [Theory]
    [InlineData("gpt-6-astra")]
    [InlineData("openai/gpt-6-astra")]
    [InlineData(" GPT-6-ASTRA ")]
    public void AstraSupportsSelectableReasoning(string model)
    {
        Assert.True(AISettings.IsAstraModel(model));
        Assert.Contains("low", AISettings.AstraReasoningEfforts);
        Assert.Contains("ultra", AISettings.AstraReasoningEfforts);
        Assert.All(AISettings.AstraReasoningEfforts, effort => Assert.Contains(effort, AISettings.AvailableReasoningEfforts));
    }
}
