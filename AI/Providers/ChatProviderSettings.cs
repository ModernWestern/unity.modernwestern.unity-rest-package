using System;
using UnityEngine;

namespace UnityREST.AI
{
    public enum ChatProviderKind : byte
    {
        /// <summary>OpenAI Chat Completions format: OpenAI, Azure OpenAI, Ollama, LM Studio, OpenRouter, Groq, Mistral, DeepSeek, vLLM...</summary>
        [InspectorName("OpenAI compatible")]
        OpenAICompatible,
        Anthropic,
        Gemini
    }

    /// <summary>Everything needed to talk to one model. Serializable, so it can live in an asset or be built in code.</summary>
    [Serializable]
    public sealed class ChatProviderSettings
    {
        public ChatProviderKind kind;

        [Tooltip("API root. Point it at your own backend proxy in shipped builds so keys never reach players.")]
        public string baseUrl;

        public string model;

        [Secret, Tooltip("Stored in the asset and shipped with the build. Prefer the environment variable for editor tools.")]
        public string apiKey;

        [Tooltip("Read the key from this environment variable when the field above is empty (editor and desktop builds).")]
        public string apiKeyEnvironmentVariable;

        [Min(1)]
        public int maxTokens = 16000;

        [Tooltip("Send temperature. Leave off for models that reject sampling parameters (current Claude models).")]
        public bool overrideTemperature;

        [Range(0f, 2f)]
        public float temperature = 1f;

        [Tooltip("Optional reasoning effort: low, medium, high... Sent as output_config.effort (Anthropic) or reasoning_effort (OpenAI).")]
        public string effort;

        [Tooltip("OpenAI-compatible only: max_completion_tokens for OpenAI reasoning models, max_tokens for most other servers.")]
        public string maxTokensParameter = "max_tokens";

        [Min(1f)]
        public float timeoutSeconds = 120f;

        public HttpHeader[] headers = Array.Empty<HttpHeader>();

        public string ResolveApiKey()
        {
            if (!string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiKeyEnvironmentVariable))
            {
                return apiKey;
            }

            return System.Environment.GetEnvironmentVariable(apiKeyEnvironmentVariable);
        }

        public IChatProvider CreateProvider()
        {
            switch (kind)
            {
                case ChatProviderKind.Anthropic:
                    return new AnthropicChatProvider(this);
                case ChatProviderKind.Gemini:
                    return new GeminiChatProvider(this);
                default:
                    return new OpenAIChatProvider(this);
            }
        }

#region Presets

        public static ChatProviderSettings OpenAI(string model = "gpt-5-mini") =>
            new ChatProviderSettings
            {
                kind = ChatProviderKind.OpenAICompatible, baseUrl = "https://api.openai.com/v1", model = model,
                apiKeyEnvironmentVariable = "OPENAI_API_KEY", maxTokensParameter = "max_completion_tokens"
            };

        public static ChatProviderSettings Anthropic(string model = "claude-opus-5-5") =>
            new ChatProviderSettings
            {
                kind = ChatProviderKind.Anthropic, baseUrl = "https://api.anthropic.com/v1", model = model,
                apiKeyEnvironmentVariable = "ANTHROPIC_API_KEY"
            };

        public static ChatProviderSettings Gemini(string model = "gemini-2.5-flash") =>
            new ChatProviderSettings
            {
                kind = ChatProviderKind.Gemini, baseUrl = "https://generativelanguage.googleapis.com/v1beta", model = model,
                apiKeyEnvironmentVariable = "GEMINI_API_KEY"
            };

        public static ChatProviderSettings Ollama(string model = "llama3.2") =>
            new ChatProviderSettings { kind = ChatProviderKind.OpenAICompatible, baseUrl = "http://localhost:11434/v1", model = model };

        public static ChatProviderSettings LmStudio(string model) =>
            new ChatProviderSettings { kind = ChatProviderKind.OpenAICompatible, baseUrl = "http://localhost:1234/v1", model = model };

        public static ChatProviderSettings OpenRouter(string model) =>
            new ChatProviderSettings
            {
                kind = ChatProviderKind.OpenAICompatible, baseUrl = "https://openrouter.ai/api/v1", model = model,
                apiKeyEnvironmentVariable = "OPENROUTER_API_KEY"
            };

        /// <summary>Any server that speaks the OpenAI Chat Completions format.</summary>
        public static ChatProviderSettings OpenAICompatible(string baseUrl, string model, string apiKeyEnvironmentVariable = null) =>
            new ChatProviderSettings
            {
                kind = ChatProviderKind.OpenAICompatible, baseUrl = baseUrl, model = model, apiKeyEnvironmentVariable = apiKeyEnvironmentVariable
            };

#endregion
    }
}
