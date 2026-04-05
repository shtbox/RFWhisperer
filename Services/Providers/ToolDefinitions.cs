using System.Text.Json;
using System.Text.Json.Nodes;

namespace SDRSharp.RFWhisperer.Services.Providers
{
    /// <summary>
    /// SDR# tool definitions in a format-agnostic representation.
    /// Each provider converts these to its own wire format.
    /// </summary>
    public static class ToolDefinitions
    {
        public record ToolDef(
            string Name,
            string Description,
            JsonObject Properties,
            string[] Required
        );

        public static readonly IReadOnlyList<ToolDef> All = new List<ToolDef>
        {
            new("set_frequency",
                "Tune the radio to a specific frequency.",
                Props(
                    ("frequency_hz", "integer", "Frequency in Hz (e.g. 108500000 for 108.5 MHz)"),
                    ("set_center",   "boolean", "Also shift the center frequency (default true)")),
                new[] { "frequency_hz" }),

            new("set_modulation",
                "Change the demodulation mode.",
                PropsEnum("mode", "Demodulation mode", "AM","WFM","USB","LSB","DSB","CW","RAW"),
                new[] { "mode" }),

            new("set_filter_bandwidth",
                "Set the filter bandwidth in Hz. Wider captures more signal; narrower reduces noise.",
                Props(("bandwidth_hz", "integer", "Bandwidth in Hz (e.g. 200000 for WFM, 3000 for SSB)")),
                new[] { "bandwidth_hz" }),

            new("set_audio_gain",
                "Set the audio output gain in dB (0–40).",
                Props(("gain_db", "number", "Audio gain in dB")),
                new[] { "gain_db" }),

            new("set_agc",
                "Enable or disable Automatic Gain Control.",
                Props(
                    ("enabled",        "boolean", "true to enable AGC"),
                    ("threshold_dbfs", "integer", "AGC threshold in dBfs (optional)")),
                new[] { "enabled" }),

            new("set_squelch",
                "Enable/disable squelch and set its threshold.",
                Props(
                    ("enabled",        "boolean", "true to enable squelch"),
                    ("threshold_dbfs", "integer", "Threshold in dBfs below which audio is muted")),
                new[] { "enabled", "threshold_dbfs" }),

            new("get_signal_info",
                "Get the current live signal metrics (SNR, power, carrier, etc.).",
                new JsonObject(),
                Array.Empty<string>()),

            new("apply_preset",
                "Apply a complete set of optimal settings for a named service.",
                PropsEnum("preset", "Preset name",
                    "fm_broadcast","am_broadcast","aviation_am","marine_vhf",
                    "noaa_weather","amateur_ssb_hf","amateur_fm_vhf","ads_b_1090","shortwave_am"),
                new[] { "preset" }),

            new("start_radio", "Start the SDR# radio.", new JsonObject(), Array.Empty<string>()),
            new("stop_radio",  "Stop the SDR# radio.",  new JsonObject(), Array.Empty<string>()),
        };

        // ── Anthropic format ─────────────────────────────────────────────────────

        public static JsonArray ToAnthropicTools()
        {
            var arr = new JsonArray();
            foreach (var t in All)
            {
                arr.Add(new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["input_schema"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = t.Properties.DeepClone(),
                        ["required"] = JsonNode.Parse(JsonSerializer.Serialize(t.Required))
                    }
                });
            }
            return arr;
        }

        // ── OpenAI-compatible format ─────────────────────────────────────────────

        public static JsonArray ToOpenAITools()
        {
            var arr = new JsonArray();
            foreach (var t in All)
            {
                arr.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = t.Name,
                        ["description"] = t.Description,
                        ["parameters"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = t.Properties.DeepClone(),
                            ["required"] = JsonNode.Parse(JsonSerializer.Serialize(t.Required))
                        }
                    }
                });
            }
            return arr;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static JsonObject Props(params (string name, string type, string desc)[] fields)
        {
            var obj = new JsonObject();
            foreach (var (name, type, desc) in fields)
                obj[name] = new JsonObject { ["type"] = type, ["description"] = desc };
            return obj;
        }

        private static JsonObject PropsEnum(string propName, string desc, params string[] values)
        {
            var obj = new JsonObject();
            obj[propName] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = desc,
                ["enum"] = JsonNode.Parse(JsonSerializer.Serialize(values))
            };
            return obj;
        }
    }
}
