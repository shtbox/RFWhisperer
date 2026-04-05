using SDRSharp.Common;

namespace SDRSharp.RFWhisperer.Services
{
    /// <summary>
    /// Reads FFT snapshot data from SDR#'s ISharpControl.GetSpectrumSnapshot
    /// and converts it into frequency-tagged peak detections.
    ///
    /// One snapshot covers the entire RF display bandwidth — for AirSpy R2
    /// that's up to ~6 MHz, for AirSpy HF+ up to 768 kHz. Much faster than
    /// stepping the VFO because no hardware retuning is required.
    /// </summary>
    public class SpectrumReader
    {
        public record Peak(long FrequencyHz, float PowerDb)
        {
            public string Label => FrequencyHz >= 1_000_000
                ? $"{FrequencyHz / 1_000_000.0:F4} MHz"
                : $"{FrequencyHz / 1_000.0:F3} kHz";
        }

        // dB range to map snapshot values into
        private const float SNAP_MIN_DB = -120f;
        private const float SNAP_MAX_DB = 0f;

        // Minimum gap between two adjacent peaks (bins) to avoid duplicates
        private const int MIN_PEAK_SEPARATION_BINS = 3;

        private float[] _buffer = new float[4096];

        /// <summary>
        /// Take one snapshot and return all peaks above <paramref name="thresholdDb"/>.
        /// </summary>
        public List<Peak> GetPeaks(ISharpControl sdr, float thresholdDb)
        {
            int resolution = Math.Max(sdr.FFTResolution, 512);
            if (_buffer.Length != resolution)
                _buffer = new float[resolution];

            sdr.GetSpectrumSnapshot(_buffer, SNAP_MIN_DB, SNAP_MAX_DB);

            long   center  = sdr.CenterFrequency;
            int    bw      = sdr.RFDisplayBandwidth;
            double hzPerBin = (double)bw / _buffer.Length;
            long   startHz  = center - bw / 2;

            var peaks = new List<Peak>();
            int lastPeakBin = -MIN_PEAK_SEPARATION_BINS - 1;

            for (int i = 1; i < _buffer.Length - 1; i++)
            {
                // Convert normalised 0..1 value back to dBfs
                float db = SNAP_MIN_DB + _buffer[i] * (SNAP_MAX_DB - SNAP_MIN_DB);

                if (db < thresholdDb) continue;

                // Local maximum check
                if (_buffer[i] <= _buffer[i - 1] || _buffer[i] <= _buffer[i + 1]) continue;

                // Enforce minimum separation
                if (i - lastPeakBin < MIN_PEAK_SEPARATION_BINS) continue;

                long hz = startHz + (long)(i * hzPerBin);
                peaks.Add(new Peak(hz, db));
                lastPeakBin = i;
            }

            // Return sorted strongest-first
            peaks.Sort((a, b) => b.PowerDb.CompareTo(a.PowerDb));
            return peaks;
        }

        /// <summary>
        /// Get the raw normalised buffer (0..1) and the Hz-per-bin for callers
        /// that want to do their own analysis (e.g. the activity monitor diff).
        /// </summary>
        public (float[] buffer, double hzPerBin, long startHz) GetRawSnapshot(ISharpControl sdr)
        {
            int resolution = Math.Max(sdr.FFTResolution, 512);
            if (_buffer.Length != resolution)
                _buffer = new float[resolution];

            sdr.GetSpectrumSnapshot(_buffer, SNAP_MIN_DB, SNAP_MAX_DB);

            long   center  = sdr.CenterFrequency;
            int    bw      = sdr.RFDisplayBandwidth;
            double hzPerBin = (double)bw / _buffer.Length;
            long   startHz  = center - bw / 2;

            return (_buffer, hzPerBin, startHz);
        }
    }
}
