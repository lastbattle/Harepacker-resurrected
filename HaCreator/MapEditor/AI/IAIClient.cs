using System;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Interface for AI API clients (OpenRouter, OpenCode, etc.)
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
        Task<string> ProcessInstructionsAsync(string mapContext, string userInstructions);

        /// <summary>
        /// Test if the API connection is valid
        /// </summary>
        /// <returns>True if connection is successful</returns>
        Task<bool> TestConnectionAsync();
    }

    /// <summary>
    /// Supported AI API providers
    /// </summary>
    public enum AIProvider
    {
        /// <summary>
        /// OpenRouter API (supports multiple models via API key)
        /// </summary>
        OpenRouter,

        /// <summary>
        /// OpenCode API (local server with OAuth-based Claude access)
        /// </summary>
        OpenCode
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
            switch (provider)
            {
                case AIProvider.OpenCode:
                    return new OpenCodeClient(
                        AISettings.OpenCodeHost,
                        AISettings.OpenCodePort,
                        AISettings.OpenCodeModel,
                        AISettings.OpenCodeAutoStart);

                case AIProvider.OpenRouter:
                default:
                    return new OpenRouterClient(
                        AISettings.ApiKey,
                        AISettings.Model);
            }
        }

        /// <summary>
        /// Create an AI client with explicit configuration (for testing/override)
        /// </summary>
        public static IAIClient CreateOpenRouter(string apiKey, string model)
        {
            return new OpenRouterClient(apiKey, model);
        }

        /// <summary>
        /// Create an OpenCode client with explicit configuration
        /// </summary>
        public static IAIClient CreateOpenCode(string host, int port, string model = null, bool autoStart = true)
        {
            return new OpenCodeClient(host, port, model, autoStart);
        }

        /// <summary>
        /// Cleanup any managed resources (e.g., auto-started OpenCode server).
        /// Call this when the application or map editor is closing.
        /// </summary>
        public static void Cleanup()
        {
            OpenCodeClient.StopServer();
        }
    }
}
