using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SDRSharp.RFWhisperer.Models;

namespace SDRSharp.RFWhisperer.Services.Providers
{
    /// <summary>
    /// Anthropic Messages API provider (Claude models).
    /// Supports tool use via the native Anthropic tool_use / tool_result protocol.
    /// </summary>
    public class AnthropicProvider : ILLMProvider, IDisposable
    {
        private const string API_URL = "https://api.anthropic.com/v1/messages";
        private const string ANTHROPIC_VERSION = "2023-06-01";
        private const int MAX_TOKENS = 2048;
        private const int MAX_HISTORY = 20;

        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
        private readonly List<JsonObject> _history = new();

        private ProviderConfig _config = new("", "", "claude-opus-4-6", true);

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_config.ApiKey);

        public void Configure(ProviderConfig config) => _config = config;
        public void ClearHistory() => _history.Clear();

        public async Task<string> SendMessageAsync(
            string userMessage,
            SignalContext ctx,
            Func<string, JsonObject, Task<string>> toolExecutor,
            CancellationToken ct = default)
        {
            _history.Add(new JsonObject { ["role"] = "user", ["content"] = userMessage });
            TrimHistory();

            string system = SystemPrompt.Build(ctx, _config.BeginnerMode);

            for (int i = 0; i < 10; i++)
            {
                var body = new JsonObject
                {
                    ["model"]      = _config.Model,
                    ["max_tokens"] = MAX_TOKENS,
                    ["system"]     = system,
                    ["tools"]      = ToolDefinitions.ToAnthropicTools(),
                    ["messages"]   = JsonNode.Parse(JsonSerializer.Serialize(_history))
                };

                var response = await PostAsync(body, ct);
                string stopReason = response["stop_reason"]?.GetValue<string>() ?? "end_turn";
                var content = response["content"]?.AsArray() ?? new JsonArray();

                _history.Add(new JsonObject
                {
                    ["role"]    = "assistant",
                    ["content"] = JsonNode.Parse(JsonSerializer.Serialize(content))!
                });

                var textParts = new List<string>();
                var toolCalls = new List<(string id, string name, JsonObject input)>();

                foreach (var block in content)
                {
                    if (block == null) continue;
                    if (block["type"]?.GetValue<string>() == "text")
                        textParts.Add(block["text"]?.GetValue<string>() ?? "");
                    else if (block["type"]?.GetValue<string>() == "tool_use")
                        toolCalls.Add((
                            block["id"]?.GetValue<string>() ?? "",
                            block["name"]?.GetValue<string>() ?? "",
                            block["input"]?.AsObject() ?? new JsonObject()));
                }

                if (stopReason == "tool_use" && toolCalls.Count > 0)
                {
                    var results = new JsonArray();
                    foreach (var (id, name, input) in toolCalls)
                    {
                        string result = await toolExecutor(name, input);
                        results.Add(new JsonObject
                        {
                            ["type"]        = "tool_result",
                            ["tool_use_id"] = id,
                            ["content"]     = result
                        });
                    }
                    _history.Add(new JsonObject { ["role"] = "user", ["content"] = results });
                    continue;
                }

                return string.Join("\n\n", textParts).Trim();
            }

            return "Unable to complete after multiple attempts.";
        }

        private async Task<JsonObject> PostAsync(JsonObject body, CancellationToken ct)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, API_URL)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("x-api-key", _config.ApiKey);
            req.Headers.Add("anthropic-version", ANTHROPIC_VERSION);

            var res = await _http.SendAsync(req, ct);
            string text = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
            {
                string? msg = null;
                try { msg = JsonNode.Parse(text)?["error"]?["message"]?.GetValue<string>(); } catch { }
                throw new Exception($"Anthropic API {(int)res.StatusCode}: {msg ?? text}");
            }

            return JsonNode.Parse(text)?.AsObject()
                   ?? throw new Exception("Empty response from Anthropic API.");
        }

        private void TrimHistory()
        {
            while (_history.Count > MAX_HISTORY * 2)
                _history.RemoveAt(0);
        }

        public void Dispose() => _http.Dispose();
    }
}
