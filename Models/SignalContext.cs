using SDRSharp.Common;

namespace SDRSharp.RFWhisperer.Models
{
    /// <summary>
    /// Snapshot of the current SDR# state plus analyzed signal metrics.
    /// Passed to Claude as context with every message.
    /// </summary>
    public class SignalContext
    {
        // --- SDR# control state ---
        public long CenterFrequency { get; set; }
        public long TuneFrequency { get; set; }
        public int FilterBandwidth { get; set; }
        public string DetectorType { get; set; } = "WFM";
        public int AudioGain { get; set; }
        public bool UseAgc { get; set; }
        public bool IsPlaying { get; set; }
        public bool SquelchEnabled { get; set; }
        public int SquelchThreshold { get; set; }

        // --- Analyzed signal metrics (updated from IQ stream) ---
        public double SignalPowerDbfs { get; set; }
        public double NoiseFloorDbfs { get; set; }
        public double SnrDb { get; set; }
        public double PeakOffsetHz { get; set; }
        public bool CarrierDetected { get; set; }
        public string EstimatedModulation { get; set; } = "Unknown";
        public double ModulationConfidence { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Formats the context as a human-readable block for the Claude system prompt.</summary>
        public string ToContextString()
        {
            string freq = FormatFrequency(TuneFrequency);
            string center = FormatFrequency(CenterFrequency);
            string bw = FilterBandwidth >= 1000
                ? $"{FilterBandwidth / 1000.0:F1} kHz"
                : $"{FilterBandwidth} Hz";

            return $"""
                ## Current Radio State
                - Tune frequency: {freq}
                - Center frequency: {center}
                - Modulation: {DetectorType}
                - Filter bandwidth: {bw}
                - Audio gain: {AudioGain} dB
                - AGC: {(UseAgc ? "enabled" : "disabled")}
                - Squelch: {(SquelchEnabled ? $"enabled at {SquelchThreshold} dBfs" : "disabled")}
                - Radio playing: {IsPlaying}

                ## Live Signal Analysis
                - Signal power: {SignalPowerDbfs:F1} dBfs
                - Noise floor: {NoiseFloorDbfs:F1} dBfs
                - SNR: {SnrDb:F1} dB
                - Carrier detected: {CarrierDetected}
                - Peak offset: {PeakOffsetHz:F0} Hz
                - Estimated modulation: {EstimatedModulation} ({ModulationConfidence:P0} confidence)
                """;
        }

        private static string FormatFrequency(long hz)
        {
            if (hz >= 1_000_000_000) return $"{hz / 1_000_000_000.0:F4} GHz ({hz:N0} Hz)";
            if (hz >= 1_000_000) return $"{hz / 1_000_000.0:F4} MHz ({hz:N0} Hz)";
            if (hz >= 1_000) return $"{hz / 1_000.0:F3} kHz ({hz:N0} Hz)";
            return $"{hz} Hz";
        }
    }
}
