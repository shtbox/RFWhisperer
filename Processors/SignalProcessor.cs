using SDRSharp.Radio;
using SDRSharp.RFWhisperer.Services;

namespace SDRSharp.RFWhisperer.Processors
{
    /// <summary>
    /// Hooks into SDR#'s DecimatedAndFilteredIQ stream via IIQProcessor.
    /// Feeds samples to SignalAnalyzer for modulation classification.
    /// Must use unsafe code to handle SDR#'s native Complex* buffers.
    /// </summary>
    public class SignalProcessor : IIQProcessor
    {
        private readonly SignalAnalyzer _analyzer;
        private volatile bool _enabled = true;

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        // IStreamProcessor — SDR# sets this when the sample rate changes
        public double SampleRate
        {
            set => _analyzer.SetSampleRate(value);
        }

        public SignalProcessor(SignalAnalyzer analyzer)
        {
            _analyzer = analyzer;
        }

        public unsafe void Process(SDRSharp.Radio.Complex* buffer, int length)
        {
            if (!_enabled || buffer == null || length == 0) return;

            try
            {
                _analyzer.ProcessIQBlock(buffer, length);
            }
            catch
            {
                // Never throw from the audio/signal thread — SDR# would crash
            }
        }
    }
}
