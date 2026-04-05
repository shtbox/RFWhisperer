using SDRSharp.Radio;
using SDRSharp.RFWhisperer.Models;

namespace SDRSharp.RFWhisperer.Services
{
    /// <summary>
    /// Thread-safe signal metrics computed from IQ sample buffers.
    /// The processor writes here; the UI and Claude service read from here.
    /// </summary>
    public class SignalAnalyzer
    {
        private const int FFT_SIZE = 2048;
        private const double CARRIER_THRESHOLD_DB = 12.0;  // SNR to declare carrier present

        private readonly object _lock = new();
        private double _signalPowerDbfs = -100.0;
        private double _noiseFloorDbfs = -100.0;
        private double _snrDb = 0.0;
        private double _peakOffsetHz = 0.0;
        private bool _carrierDetected = false;
        private string _estimatedModulation = "Unknown";
        private double _modConfidence = 0.0;

        // Ring buffer for tracking amplitude envelope (for AM vs FM detection)
        private readonly float[] _envelopeHistory = new float[512];
        private int _envelopeIndex = 0;
        private double _sampleRate = 2_400_000;

        public void SetSampleRate(double sampleRate) => _sampleRate = sampleRate;

        /// <summary>
        /// Process a block of IQ samples. Called from the signal processor thread.
        /// Uses unsafe code for performance.
        /// </summary>
        public unsafe void ProcessIQBlock(SDRSharp.Radio.Complex* buffer, int length)
        {
            if (length < 64) return;

            // Compute power of first N samples
            float signalPower = 0f;
            float peakPower = 0f;
            int peakBin = 0;

            int analyzeLen = Math.Min(length, FFT_SIZE);

            for (int i = 0; i < analyzeLen; i++)
            {
                float mag2 = buffer[i].ModulusSquared();
                signalPower += mag2;

                if (mag2 > peakPower)
                {
                    peakPower = mag2;
                    peakBin = i;
                }

                // Track envelope for modulation classification
                _envelopeHistory[_envelopeIndex % _envelopeHistory.Length] = buffer[i].Modulus();
                _envelopeIndex++;
            }

            signalPower /= analyzeLen;

            // Estimate noise floor as bottom 30th percentile of per-sample power
            var powers = new float[analyzeLen];
            for (int i = 0; i < analyzeLen; i++)
                powers[i] = buffer[i].ModulusSquared();
            Array.Sort(powers);
            float noiseFloor = powers[(int)(analyzeLen * 0.30f)];

            double sigDbfs = PowerToDbfs(signalPower);
            double noiseDbfs = PowerToDbfs(noiseFloor);
            double snr = sigDbfs - noiseDbfs;

            // Peak frequency offset from center
            double peakOffHz = (peakBin - analyzeLen / 2.0) * (_sampleRate / analyzeLen);

            bool carrier = snr > CARRIER_THRESHOLD_DB;

            // Simple AM vs FM heuristic:
            // AM: envelope variance is proportional to modulation depth
            // FM: envelope is roughly constant, frequency varies
            (string mod, double conf) = ClassifyModulation();

            lock (_lock)
            {
                _signalPowerDbfs = sigDbfs;
                _noiseFloorDbfs = noiseDbfs;
                _snrDb = snr;
                _peakOffsetHz = peakOffHz;
                _carrierDetected = carrier;
                _estimatedModulation = mod;
                _modConfidence = conf;
            }
        }

        /// <summary>
        /// Updates all signal metrics in the context (called when SDR# VisualSNR is not available).
        /// </summary>
        public void UpdateSignalContext(SignalContext ctx)
        {
            lock (_lock)
            {
                ctx.SignalPowerDbfs   = _signalPowerDbfs;
                ctx.NoiseFloorDbfs   = _noiseFloorDbfs;
                ctx.SnrDb            = _snrDb;
                ctx.PeakOffsetHz     = _peakOffsetHz;
                ctx.CarrierDetected  = _carrierDetected;
                ctx.EstimatedModulation = _estimatedModulation;
                ctx.ModulationConfidence = _modConfidence;
                ctx.Timestamp = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Updates only the modulation classification (power/SNR come from ISharpControl.VisualSNR).
        /// </summary>
        public void UpdateModulationContext(SignalContext ctx)
        {
            lock (_lock)
            {
                ctx.EstimatedModulation  = _estimatedModulation;
                ctx.ModulationConfidence = _modConfidence;
                ctx.PeakOffsetHz         = _peakOffsetHz;
                ctx.Timestamp = DateTime.UtcNow;
            }
        }

        private (string modulation, double confidence) ClassifyModulation()
        {
            if (_envelopeIndex < _envelopeHistory.Length)
                return ("Unknown", 0.0);

            // Compute envelope mean and variance
            float sum = 0f, sum2 = 0f;
            for (int i = 0; i < _envelopeHistory.Length; i++)
            {
                sum += _envelopeHistory[i];
                sum2 += _envelopeHistory[i] * _envelopeHistory[i];
            }
            float mean = sum / _envelopeHistory.Length;
            float variance = sum2 / _envelopeHistory.Length - mean * mean;
            float normalizedVariance = mean > 0.001f ? variance / (mean * mean) : 0f;

            // High envelope variance → AM-like
            // Low envelope variance → FM/CW-like
            if (normalizedVariance > 0.25f)
                return ("AM", Math.Min(normalizedVariance * 2, 1.0));
            else if (normalizedVariance < 0.05f)
                return ("FM/CW", 0.8);
            else
                return ("Mixed/Data", 0.5);
        }

        private static double PowerToDbfs(double linearPower)
        {
            if (linearPower <= 0) return -120.0;
            return 10.0 * Math.Log10(linearPower);
        }
    }
}
