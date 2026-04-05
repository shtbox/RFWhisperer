using System.Text.Json.Nodes;
using SDRSharp.RFWhisperer.Models;
using SDRSharp.RFWhisperer.Services.Providers;

namespace SDRSharp.RFWhisperer.Services
{
    public enum ProviderType { Anthropic, OpenAICompatible }

    /// <summary>
    /// Coordinates LLM providers. Delegates to AnthropicProvider or
    /// OpenAICompatProvider based on the current configuration.
    /// </summary>
    public class LLMService : IDisposable
    {
        private readonly AnthropicProvider _anthropic = new();
        private readonly OpenAICompatProvider _openai = new();

        private ProviderType _activeProvider = ProviderType.Anthropic;

        public Func<string, JsonObject, Task<string>>? OnToolCall { get; set; }

        public bool IsConfigured => ActiveProvider.IsConfigured;

        private ILLMProvider ActiveProvider =>
            _activeProvider == ProviderType.Anthropic ? _anthropic : _openai;

        public void Configure(
            ProviderType providerType,
            string apiKey,
            string baseUrl,
            string model,
            bool beginnerMode)
        {
            _activeProvider = providerType;

            var cfg = new ProviderConfig(apiKey, baseUrl, model, beginnerMode);

            if (providerType == ProviderType.Anthropic)
                _anthropic.Configure(cfg);
            else
                _openai.Configure(cfg);
        }

        public void ClearHistory()
        {
            _anthropic.ClearHistory();
            _openai.ClearHistory();
        }

        public Task<string> SendMessageAsync(
            string userMessage,
            SignalContext ctx,
            CancellationToken ct = default)
        {
            if (!IsConfigured)
                throw new InvalidOperationException(
                    "Not configured. Enter your API key (and base URL for local models) in the Settings tab.");

            return ActiveProvider.SendMessageAsync(
                userMessage, ctx,
                OnToolCall ?? ((_, _) => Task.FromResult("Tool execution not wired up.")),
                ct);
        }

        public void Dispose()
        {
            _anthropic.Dispose();
            _openai.Dispose();
        }
    }
}
