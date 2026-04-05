using SDRSharp.Common;
using SDRSharp.RFWhisperer.Models;

namespace SDRSharp.RFWhisperer.Services
{
    /// <summary>
    /// Covers a wide band by stepping the CENTER FREQUENCY (not the VFO) through it,
    /// grabbing an FFT snapshot at each position instantly — no dwell delay needed.
    ///
    /// For an AirSpy R2 with 6 MHz display bandwidth, the aviation band (118–137 MHz)
    /// is covered in 4 center positions, taking ~200 ms total vs many minutes with
    /// VFO dwell-based scanning.
    ///
    /// The VFO is only moved when a peak is found (optional auto-tune on hit).
    /// </summary>
    public class FastSweepScanner : IDisposable
    {
        // ── Events ────────────────────────────────────────────────────────────────
        public event Action<ScanResult>? PeakFound;
        public event Action<long>? CenterChanged;
        public event Action<IReadOnlyList<ScanResult>>? SweepComplete;

        // ── Config ────────────────────────────────────────────────────────────────
        public long  StartHz       { get; set; } = 118_000_000;
        public long  EndHz         { get; set; } = 137_000_000;
        public float ThresholdDb   { get; set; } = -70f;
        public int   SettleMs      { get; set; } = 80;   // time to let center settle before snapshot
        public bool  RepeatSweep   { get; set; } = false;
        public bool  AutoTuneOnHit { get; set; } = false;

        // ── State ─────────────────────────────────────────────────────────────────
        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

        private readonly ISharpControl _sdr;
        private readonly SpectrumReader _reader = new();
        private CancellationTokenSource? _cts;
        private long _savedCenter;
        private long _savedVfo;

        public FastSweepScanner(ISharpControl sdr)
        {
            _sdr = sdr;
        }

        public void Start()
        {
            if (IsRunning) return;
            _savedCenter = _sdr.CenterFrequency;
            _savedVfo    = _sdr.Frequency;
            _cts = new CancellationTokenSource();
            _ = RunAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts = null;
            // Restore original tuning
            try
            {
                _sdr.CenterFrequency = _savedCenter;
                _sdr.Frequency       = _savedVfo;
            }
            catch { }
        }

        private async Task RunAsync(CancellationToken ct)
        {
            do
            {
                var hits = new List<ScanResult>();
                int bw = _sdr.RFDisplayBandwidth;

                if (bw <= 0) bw = 2_400_000;   // fallback if not playing

                // Step centers so each position overlaps slightly with the next
                long step = (long)(bw * 0.85);
                long center = StartHz + bw / 2;

                while (center - bw / 2 <= EndHz && !ct.IsCancellationRequested)
                {
                    _sdr.CenterFrequency = center;
                    CenterChanged?.Invoke(center);

                    // Wait for SDR# to update the FFT at the new center
                    await Task.Delay(SettleMs, ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested) break;

                    // Grab peaks from the full visible bandwidth
                    var peaks = _reader.GetPeaks(_sdr, ThresholdDb);

                    foreach (var peak in peaks)
                    {
                        // Only record peaks within our target range
                        if (peak.FrequencyHz < StartHz || peak.FrequencyHz > EndHz) continue;

                        var result = new ScanResult(peak.FrequencyHz, peak.PowerDb, peak.PowerDb, DateTime.Now);
                        hits.Add(result);
                        PeakFound?.Invoke(result);

                        if (AutoTuneOnHit)
                            _sdr.Frequency = peak.FrequencyHz;
                    }

                    center += step;
                }

                // Deduplicate hits — same frequency found in overlapping windows
                var deduped = hits
                    .GroupBy(h => (h.FrequencyHz / 10_000) * 10_000)
                    .Select(g => g.OrderByDescending(h => h.SignalDbfs).First())
                    .OrderBy(h => h.FrequencyHz)
                    .ToList();

                SweepComplete?.Invoke(deduped);

            } while (RepeatSweep && !ct.IsCancellationRequested);

            // Restore original tuning when not repeating
            if (!RepeatSweep)
            {
                try
                {
                    _sdr.CenterFrequency = _savedCenter;
                    _sdr.Frequency       = _savedVfo;
                }
                catch { }
                _cts = null;
            }
        }

        public void Dispose() => Stop();
    }
}
