using System.Text.Json;
using SDRSharp.RFWhisperer.Services;

namespace SDRSharp.RFWhisperer.Services
{
    public record PluginSettingsData(
        string ProviderType,   // "anthropic" | "openai"
        string ApiKey,
        string BaseUrl,
        string Model,
        bool BeginnerMode,
        int TimeoutSeconds = 120   // default 2 min; local models can be slow
    );

    public static class PluginSettings
    {
        private static string SettingsPath =>
            Path.Combine(
                Path.GetDirectoryName(typeof(PluginSettings).Assembly.Location) ?? ".",
                "SDRSharp.RFWhisperer.json");

        public static PluginSettingsData Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return JsonSerializer.Deserialize<PluginSettingsData>(
                               File.ReadAllText(SettingsPath))
                           ?? Default();
            }
            catch { }
            return Default();
        }

        public static void Save(PluginSettingsData data)
        {
            try
            {
                File.WriteAllText(SettingsPath,
                    JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public static ProviderType ParseProviderType(string s) =>
            s == "openai" ? ProviderType.OpenAICompatible : ProviderType.Anthropic;

        public static string SerializeProviderType(ProviderType t) =>
            t == ProviderType.OpenAICompatible ? "openai" : "anthropic";

        private static PluginSettingsData Default() =>
            new("anthropic", "", "", "claude-opus-4-6", true, 120);
    }
}
