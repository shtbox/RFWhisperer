# SDR# RF Whisperer Plugin

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

An AI-powered plugin for [SDR#](https://airspy.com/download/) that gives you a conversational radio assistant directly inside the app. Whether you're just getting started with software defined radio or you're an experienced operator, the assistant can identify signals, tune to frequencies, diagnose reception problems, and configure SDR# — all through natural language.

**Runs fully offline.** You can use local LLMs running on your own machine — no internet connection, no API key, no data leaving your computer. Tools like [Ollama](https://ollama.com), LM Studio, and llama.cpp are supported out of the box. Cloud providers (Anthropic, OpenAI, Groq, OpenRouter) are also supported if you prefer them.

---

## What It Can Do

### Talk to Your Radio
Type a question or instruction in plain English and the AI will respond — and act. It doesn't just describe what settings to change, it changes them for you in real time.

> *"Tune to my local FM news station"*
> *"What's on 121.5 MHz?"*
> *"Something sounds wrong with this signal, can you diagnose it?"*
> *"Set everything up for listening to aircraft"*

### Identify Signals
The assistant knows the frequency allocations for aviation, marine, amateur radio, shortwave, public safety, weather, ISM bands, satellites, and more. Tell it what frequency you're on and it will tell you what's likely there, what modulation to use, and what to listen for.

### Diagnose Reception Problems
The plugin reads your live signal metrics — SNR, signal power, noise floor, carrier detection — and feeds them to the AI with every message. When something sounds wrong, ask why. It can spot common issues like wrong bandwidth, missing AGC, incorrect modulation mode, or a noisy signal and fix them automatically.

### Apply Presets
One command applies a complete set of optimised settings for a service:

| Preset | Frequency Range | Mode |
|---|---|---|
| FM Broadcast | 87.5 – 108 MHz | WFM, 200 kHz |
| AM Broadcast | 530 – 1710 kHz | AM, 10 kHz |
| Aviation Comms | 118 – 137 MHz | AM, 8 kHz |
| Marine VHF | 156 – 174 MHz | NFM, 15 kHz, squelch |
| NOAA Weather | 162.4 – 162.55 MHz | WFM, tunes automatically |
| Amateur SSB/HF | HF bands | USB, 3 kHz |
| Amateur FM/VHF | 144 – 148 MHz | NFM, 12.5 kHz, squelch |
| ADS-B Aircraft | 1090 MHz | RAW, tunes automatically |
| Shortwave AM | 2 – 30 MHz | AM, 8 kHz |

### Control SDR# Directly
The AI can set any of these without you touching the interface:

- **Frequency** — tune and center frequency
- **Modulation** — AM, WFM, USB, LSB, DSB, CW, RAW
- **Filter bandwidth** — from 500 Hz CW to 200 kHz wideband FM
- **Audio gain** — 0–40 dB
- **AGC** — enable/disable, threshold
- **Squelch** — enable/disable, threshold
- **Start / stop** the radio

### Beginner and Advanced Modes
Switch between modes in Settings:
- **Beginner** — the AI explains what it's doing and why, avoids jargon, teaches as it helps
- **Advanced** — terse and technical, no hand-holding

### Works With Any AI Model
Not locked to one provider. Use whatever model you prefer:

| Provider | Notes |
|---|---|
| **Anthropic Claude** | Best overall performance; tool use is reliable |
| **OpenAI** | GPT-4o and later work well |
| **Groq** | Fast inference, good for quick queries |
| **Ollama** (local) | Run models fully offline on your own machine |
| **Docker Model Runner** (local) | Docker's built-in model runner — same API as Ollama |
| **LM Studio** (local) | Local models with a GUI server |
| **llama.cpp** (local) | Lightweight local inference |
| **OpenRouter** | Access many models through one API |
| Any OpenAI-compatible server | One base URL field covers them all |

---

## Getting Started

### 1. Install SDR#
Download SDR# from [airspy.com/download](https://airspy.com/download/) and extract it to a folder such as `C:\SDRSharp\`.

### 2. Copy the Plugin
Copy `SDRSharp.RFWhisperer.dll` into the `Plugins\` folder inside your SDR# directory.

### 3. Register the Plugin
Open `SDRSharp.exe.config` (in your SDR# folder) and add this line inside the `<configuration>` block:

```xml
<add key="plugin.RFWhisperer" value="SDRSharp.RFWhisperer.RFWhispererPlugin,SDRSharp.RFWhisperer" />
```

### 4. Launch SDR#
The **RF Whisperer** panel appears under the Plugins menu. Open it and go to the **Settings** tab.

### 5. Configure Your AI Provider

**Anthropic Claude (cloud)**
- Select `Anthropic (Claude)`
- Paste your API key from [console.anthropic.com](https://console.anthropic.com)
- Enter a model name: `claude-opus-4-6` or `claude-sonnet-4-6`

**Local model via Ollama**
- Install Ollama and pull a model: `ollama pull llama3.1`
- Select `OpenAI Compatible`
- Click the **Ollama** preset button (fills `http://localhost:11434/v1`)
- Enter the model name: `llama3.1`
- Leave API key blank

**Local model via Docker Model Runner**
- Requires Docker Desktop 4.40+ with the Model Runner feature enabled
- Pull a model: `docker model pull ai/llama3.2`
- Select `OpenAI Compatible`
- Set base URL to `http://localhost:12434/engines/llama.cpp/v1`
- Enter the model name exactly as pulled, e.g. `ai/llama3.2`
- Leave API key blank
- The Docker Model Runner exposes the same OpenAI-compatible API as Ollama

**Groq (fast cloud inference)**
- Select `OpenAI Compatible`
- Click the **Groq** preset button
- Paste your Groq API key
- Enter a model: `llama-3.3-70b-versatile` or `mixtral-8x7b-32768`

Click **Test Connection** to verify before saving.

---

## Using the Plugin

### Chat Tab
Type anything in the input box and press **Enter** (or Shift+Enter for a new line). Use the quick buttons at the top for the most common tasks:

- **Identify** — tells you what signal you're receiving
- **Diagnose** — checks signal quality and fixes settings
- **Best Settings** — applies optimal settings for the current frequency

The status bar at the top shows your current frequency, modulation mode, live SNR, and a carrier indicator dot (green = signal detected).

### Diagnostics Tab
Click **Run AI Diagnostic** for a full written report covering signal quality, whether your settings are optimal, any problems detected, and automatic fixes applied.

The quick-tune buttons let you jump to common services in one click.

### Settings Tab
- **Provider** — choose Anthropic or OpenAI Compatible
- **Base URL** — for local/third-party servers (preset buttons fill this automatically)
- **API Key** — required for cloud providers; leave blank for local models
- **Model** — the exact model name to use
- **Mode** — Beginner or Advanced
- **Save Settings** — persists settings to `SDRSharp.RFWhisperer.json` next to the DLL
- **Test Connection** — sends a quick test message and shows the result

---

## Notes on Local Models

Tool calling (the mechanism that lets the AI actually change settings) requires a model that supports function calling. Models that work well:

- `llama3.1`, `llama3.2` (Ollama)
- `mistral-nemo`, `mistral-small` (Ollama / OpenRouter)
- `qwen2.5`, `qwen2.5-coder` (Ollama)
- `command-r` (OpenRouter / Groq)

If a model doesn't support tool calling it will still respond in text — you'll get advice but the settings won't change automatically.

---

## Technical Details

### Architecture

```
SDRSharp.RFWhisperer.dll
├── RFWhispererPlugin.cs          ISharpPlugin + ICanLazyLoadGui entry point
├── Processors/
│   └── SignalProcessor.cs        IIQProcessor — hooks into DecimatedAndFilteredIQ stream
├── Services/
│   ├── LLMService.cs             Provider coordinator
│   ├── SignalAnalyzer.cs         Real-time IQ analysis (SNR, modulation classification)
│   ├── FrequencyDatabase.cs      30+ frequency band definitions
│   ├── PluginSettings.cs         JSON settings persistence
│   └── Providers/
│       ├── ILLMProvider.cs       Provider interface
│       ├── AnthropicProvider.cs  Anthropic Messages API (tool_use protocol)
│       ├── OpenAICompatProvider  OpenAI /v1/chat/completions (function calling)
│       ├── SystemPrompt.cs       Shared prompt builder
│       └── ToolDefinitions.cs    Tool schemas in Anthropic and OpenAI wire formats
└── UI/
    └── RFWhispererPanel.cs       WinForms panel — Chat, Diagnostics, Settings tabs
```

### SDR# Integration

The plugin implements the `.NET 9` SDR# plugin SDK interfaces:

| Interface | Purpose |
|---|---|
| `ISharpPlugin` | Entry point — `Initialize()` and `Close()` |
| `ICanLazyLoadGui` | Panel is created on first open, not at startup |
| `IExtendedNameProvider` | Registers under the `AI` category in the Plugins menu |
| `ISupportStatus` | Reports active state to SDR# |

The `SignalProcessor` implements `IIQProcessor` and is registered via `RegisterStreamHook(processor, ProcessorType.DecimatedAndFilteredIQ)`. It receives complex IQ samples in real time and feeds them to `SignalAnalyzer` for modulation classification.

Live signal metrics (`VisualSNR`, `VisualPeak`, `VisualFloor`) are read directly from `ISharpControl` — SDR#'s own computed values — which are more accurate than what the plugin can derive from raw samples.

### AI Tool Protocol

The AI has access to 10 tools. Both providers receive the same tool definitions, translated to their respective wire formats by `ToolDefinitions.cs`:

| Tool | What It Does |
|---|---|
| `set_frequency` | Tunes frequency and optionally shifts center |
| `set_modulation` | Changes detector type |
| `set_filter_bandwidth` | Sets filter BW in Hz |
| `set_audio_gain` | Sets audio gain 0–40 dB |
| `set_agc` | Enables/disables AGC with optional threshold |
| `set_squelch` | Enables/disables squelch with threshold |
| `get_signal_info` | Returns current `SignalContext` as text |
| `apply_preset` | Applies a named preset (9 presets) |
| `start_radio` | Calls `ISharpControl.StartRadio()` |
| `stop_radio` | Calls `ISharpControl.StopRadio()` |

The agentic loop runs up to 10 iterations per message — the AI can chain multiple tool calls (e.g. set frequency, then set mode, then get signal info to verify) before returning its final response.

### Build Requirements

- .NET 9 SDK
- Windows (WinForms)
- SDR# plugin SDK (`SDRSharp.Common.dll`, `SDRSharp.Radio.dll`, `SDRSharp.PanView.dll`)

```bash
dotnet build RFWhisperer.csproj /p:SdrSharpSdk="path\to\sdk\lib"
```

The `SdrSharpSdk` property defaults to the path set in the `.csproj` file. The built DLL does not include the SDR# assemblies (`Private=false`) — they are provided by the host at runtime.

Settings are stored as JSON at `<plugin folder>\SDRSharp.RFWhisperer.json`. The API key is stored locally and is only ever transmitted to the configured API endpoint.

---

## License

MIT — see [LICENSE](LICENSE) for details.
