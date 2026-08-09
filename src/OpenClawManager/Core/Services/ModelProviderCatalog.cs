using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public static class ModelProviderCatalog
{
    public static IReadOnlyList<ModelProvider> All { get; } = new[]
    {
        new ModelProvider("openai", "OpenAI", "openai-api-key", "OPENAI_API_KEY"),
        new ModelProvider("anthropic", "Anthropic", "anthropic-api-key", "ANTHROPIC_API_KEY"),
        new ModelProvider("google", "Google Gemini", "google-gemini-api-key", "GEMINI_API_KEY"),
        new ModelProvider("deepseek", "DeepSeek", "custom-api-key", "DEEPSEEK_API_KEY", true),
        new ModelProvider("openrouter", "OpenRouter", "custom-api-key", "OPENROUTER_API_KEY", true),
        new ModelProvider("custom", "自定义 OpenAI 兼容接口", "custom-api-key", "CUSTOM_API_KEY", true)
    };

    public static ModelProvider? Find(string id)
        => All.FirstOrDefault(provider => string.Equals(provider.Id, id, StringComparison.OrdinalIgnoreCase));
}
