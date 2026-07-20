using System;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Interface for AI API clients.
    /// Abstracts the underlying API provider for map editing AI functionality.
    /// </summary>
    public interface IAIClient
    {
        /// <summary>
        /// Process natural language instructions using function calling.
        /// Handles multi-turn conversations for query functions.
        /// </summary>
        /// <param name="mapContext">The current map state in AI-readable format</param>
        /// <param name="userInstructions">Natural language instructions from the user</param>
        /// <returns>List of executable map commands as a string</returns>
        Task<string> ProcessInstructionsAsync(
            string mapContext,
            string userInstructions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Test if the API connection is valid
        /// </summary>
        /// <returns>True if connection is successful</returns>
        Task<bool> TestConnectionAsync();
    }

    /// <summary>
    /// AI integration mode. The endpoint itself determines the provider.
    /// </summary>
    public enum AIProvider
    {
        /// <summary>
        /// OpenAI-compatible HTTP API. OpenRouter is the default preset.
        /// </summary>
        OpenAICompatible
    }

    /// <summary>
    /// Factory for creating AI client instances based on configuration
    /// </summary>
    public static class AIClientFactory
    {
        /// <summary>
        /// Create an AI client based on current settings
        /// </summary>
        /// <returns>An IAIClient implementation for the configured provider</returns>
        public static IAIClient Create()
        {
            return Create(AISettings.Provider);
        }

        /// <summary>
        /// Create an AI client for a specific provider
        /// </summary>
        /// <param name="provider">The AI provider to use</param>
        /// <returns>An IAIClient implementation</returns>
        public static IAIClient Create(AIProvider provider)
        {
            return new OpenAICompatibleClient(AISettings.CreateOptions());
        }

        /// <summary>
        /// Create an AI client with explicit configuration (for testing/override)
        /// </summary>
        public static IAIClient CreateOpenAICompatible(string baseUrl, string apiKey, string model,
            AIEndpointProtocol protocol = AIEndpointProtocol.ChatCompletions)
        {
            return new OpenAICompatibleClient(new OpenAICompatibleOptions
            {
                BaseUrl = baseUrl,
                ApiKey = apiKey,
                Model = model,
                Protocol = protocol
            });
        }
    }
}
