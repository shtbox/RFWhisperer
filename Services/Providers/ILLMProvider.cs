using System.Text.Json.Nodes;
using SDRSharp.RFWhisperer.Models;

namespace SDRSharp.RFWhisperer.Services.Providers
{
    /// <summary>
    /// Common config passed to any provider.
    /// </summary>
    public record ProviderConfig(
        string ApiKey,
        string BaseUrl,
        string Model,
        bool BeginnerMode
    );

    /// <summary>
    /// Abstraction over any LLM backend (Anthropic, OpenAI-compatible, local, etc.).
    /// </summary>
    public interface ILLMProvider
    {
        bool IsConfigured { get; }

        void Configure(ProviderConfig config);
        void ClearHistory();

        /// <summary>
        /// Send a user message and return the final text response.
        /// The provider calls <paramref name="toolExecutor"/> when the model invokes a tool.
        /// </summary>
        Task<string> SendMessageAsync(
            string userMessage,
            SignalContext ctx,
            Func<string, JsonObject, Task<string>> toolExecutor,
            CancellationToken ct = default);
    }
}
