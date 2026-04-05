using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SDRSharp.RFWhisperer.Models;

namespace SDRSharp.RFWhisperer.Services.Providers
{
    /// <summary>
    /// OpenAI-compatible chat completions provider.
    /// Works with: OpenAI, Ollama, LM Studio, llama.cpp, Groq, OpenRouter,
    /// LocalAI, Jan, and any other server that exposes /v1/chat/completions.
    ///
    /// Note: tool/function calling support varies by local model. If the model
    /// doesn't call tools the plugin degrades gracefully to plain text responses.
    /// </summary>
    public class OpenAICompatProvider : ILLMProvider, IDisposable
    {
        private const int MAX_TOKENS = 2048;
        private const int MAX_HISTORY = 20;

        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(120) };

        // OpenAI-format history: includes system, user, assistant, tool messages
        private readonly List<JsonObject> _history = new();

        private ProviderConfig _config = new("", "http://localhost:11434/v1", "llama3", true);

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_config.BaseUrl) &&
            !string.IsNullOrWhiteSpace(_config.Model);

        public void Configure(ProviderConfig config) => _config = config;
        public void ClearHistory() => _history.Clear();

        public async Task<string> SendMessageAsync(
            string userMessage,
            SignalContext ctx,
            Func<string, JsonObject, Task<string>> toolExecutor,
            CancellationToken ct = default)
        {
            // Build fresh system message each turn (context changes)
            var messages = new List<JsonObject>
            {
                new() { ["role"] = "system", ["content"] = SystemPrompt.Build(ctx, _config.BeginnerMode) }
            };
            messages.AddRange(_history);
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = userMessage });

            // Also persist to history for continuity
            _history.Add(new JsonObject { ["role"] = "user", ["content"] = userMessage });
            TrimHistory();

            for (int i = 0; i < 10; i++)
            {
                var body = BuildRequestBody(messages);
                var response = await PostAsync(body, ct);

                var choice = response["choices"]?[0];
                if (choice == null) throw new Exception("No choices in response.");

                string finishReason = choice["finish_reason"]?.GetValue<string>() ?? "stop";
                var message = choice["message"]?.AsObject() ?? new JsonObject();

                // Add assistant message to both rolling lists
                _history.Add(message.DeepClone().AsObject());
                messages.Add(message.DeepClone().AsObject());

                // Check for tool calls
                var toolCalls = message["tool_calls"]?.AsArray();
                if ((finishReason == "tool_calls" || toolCalls?.Count > 0) && toolCalls != null)
                {
                    foreach (var tc in toolCalls)
                    {
                        if (tc == null) continue;
                        string callId   = tc["id"]?.GetValue<string>() ?? "";
                        string funcName = tc["function"]?["name"]?.GetValue<string>() ?? "";
                        string argsJson = tc["function"]?["arguments"]?.GetValue<string>() ?? "{}";

                        JsonObject args;
                        try { args = JsonNode.Parse(argsJson)?.AsObject() ?? new JsonObject(); }
                        catch { args = new JsonObject(); }

                        string result = await toolExecutor(funcName, args);

                        var toolMsg = new JsonObject
                        {
                            ["role"]         = "tool",
                            ["tool_call_id"] = callId,
                            ["content"]      = result
                        };
                        _history.Add(toolMsg.DeepClone().AsObject());
                        messages.Add(toolMsg);
                    }
                    continue;
                }

                // Plain text response
                string? text = message["content"]?.GetValue<string>();
                return text?.Trim() ?? "";
            }

            return "Unable to complete after multiple attempts.";
        }

        private JsonObject BuildRequestBody(List<JsonObject> messages)
        {
            var body = new JsonObject
            {
                ["model"]       = _config.Model,
                ["max_tokens"]  = MAX_TOKENS,
                ["messages"]    = JsonNode.Parse(JsonSerializer.Serialize(messages)),
                ["tools"]       = ToolDefinitions.ToOpenAITools(),
                ["tool_choice"] = "auto"
            };

            // Some local servers don't support max_tokens; they use max_completion_tokens
            // We'll keep max_tokens — servers that don't understand it just ignore it.
            return body;
        }

        private async Task<JsonObject> PostAsync(JsonObject body, CancellationToken ct)
        {
            string url = _config.BaseUrl.TrimEnd('/') + "/chat/completions";

            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(_config.ApiKey))
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.ApiKey);

            var res = await _http.SendAsync(req, ct);
            string text = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
            {
                string? msg = null;
                try { msg = JsonNode.Parse(text)?["error"]?["message"]?.GetValue<string>(); } catch { }
                throw new Exception($"API {(int)res.StatusCode}: {msg ?? text}");
            }

            return JsonNode.Parse(text)?.AsObject()
                   ?? throw new Exception("Empty response from API.");
        }

        private void TrimHistory()
        {
            // Keep pairs of (user+assistant), not bare system messages
            while (_history.Count > MAX_HISTORY * 2)
                _history.RemoveAt(0);
        }

        public void Dispose() => _http.Dispose();
    }
}
