using SDRSharp.Common;
using SDRSharp.RFWhisperer.Models;

namespace SDRSharp.RFWhisperer.Services
{
    /// <summary>
    /// Continuously polls FFT snapshots and provides two outputs:
    ///
    /// 1. ActivePeaks — all signals currently visible above threshold (updated ~10 Hz)
    /// 2. TransmissionStarted — fires when a NEW peak appears that wasn't there on the
    ///    previous frame. In Auto-Jump mode the UI tunes to that frequency immediately.
    ///
    /// Does NOT move the center or VFO — it only reads what SDR# is already showing.
    /// Combine with FastSweepScanner to cover a wider band.
    /// </summary>
    public class ActivityMonitor : IDisposable
    {
        // ── Events ────────────────────────────────────────────────────────────────
        /// <summary>Updated every poll cycle with ALL currently active peaks.</summary>
        public event Action<IReadOnlyList<SpectrumReader.Peak>>? ActivePeaksUpdated;

        /// <summary>
        /// A peak appeared that was absent (or below threshold) in the previous frame.
        /// This is the "transmission started" event for Auto-Jump.
        /// </summary>
        public event Action<SpectrumReader.Peak>? TransmissionStarted;

        // ── Config ────────────────────────────────────────────────────────────────
        public float  ThresholdDb   { get; set; } = -70f;
        public int    PollIntervalMs { get; set; } = 100;   // ~10 Hz
        public bool   AutoJump      { get; set; } = false;
        public int    MaxPeaks      { get; set; } = 30;

        // ── State ─────────────────────────────────────────────────────────────────
        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;
        public IReadOnlyList<SpectrumReader.Peak> CurrentPeaks => _currentPeaks;

        private readonly ISharpControl _sdr;
        private readonly SpectrumReader _reader = new();
        private CancellationTokenSource? _cts;
        private List<SpectrumReader.Peak> _currentPeaks = new();

        // Previous frame: set of frequency bins that were active (for spike detection)
        private readonly HashSet<long> _prevActiveHz = new();

        // Hysteresis: don't re-fire TransmissionStarted for same freq within this window
        private readonly Dictionary<long, DateTime> _recentFires = new();
        private static readonly TimeSpan REFIRE_GUARD = TimeSpan.FromSeconds(2);

        public ActivityMonitor(ISharpControl sdr)
        {
            _sdr = sdr;
        }

        public void Start()
        {
            if (IsRunning) return;
            _prevActiveHz.Clear();
            _recentFires.Clear();
            _cts = new CancellationTokenSource();
            _ = RunAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts = null;
        }

        private async Task RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var peaks = _reader.GetPeaks(_sdr, ThresholdDb);
                    if (peaks.Count > MaxPeaks) peaks = peaks.Take(MaxPeaks).ToList();

                    _currentPeaks = peaks;
                    ActivePeaksUpdated?.Invoke(peaks);

                    // Detect new transmissions (peaks not seen in previous frame)
                    var now = DateTime.UtcNow;
                    var currentHz = new HashSet<long>(peaks.Select(p => SnapToGrid(p.FrequencyHz)));

                    foreach (var peak in peaks)
                    {
                        long snapped = SnapToGrid(peak.FrequencyHz);

                        if (_prevActiveHz.Contains(snapped)) continue;

                        // Check refire guard
                        if (_recentFires.TryGetValue(snapped, out var lastFired) &&
                            now - lastFired < REFIRE_GUARD)
                            continue;

                        _recentFires[snapped] = now;
                        TransmissionStarted?.Invoke(peak);

                        if (AutoJump)
                        {
                            _sdr.Frequency = peak.FrequencyHz;
                            break; // tune to the first (strongest) new peak only
                        }
                    }

                    _prevActiveHz.Clear();
                    foreach (var hz in currentHz) _prevActiveHz.Add(hz);

                    // Prune old refire entries
                    var stale = _recentFires.Where(kv => now - kv.Value > REFIRE_GUARD * 3)
                                            .Select(kv => kv.Key).ToList();
                    foreach (var k in stale) _recentFires.Remove(k);
                }
                catch { /* never crash the monitor thread */ }

                await Task.Delay(PollIntervalMs, ct).ConfigureAwait(false);
            }
            _cts = null;
        }

        /// <summary>
        /// Round frequency to nearest channel grid (5 kHz) to avoid treating
        /// the same carrier at slightly different bins as separate signals.
        /// </summary>
        private static long SnapToGrid(long hz) => (hz / 5_000) * 5_000;

        public void Dispose() => Stop();
    }
}
