using SDRSharp.Common;
using SDRSharp.Radio;
using SDRSharp.RFWhisperer.Models;

namespace SDRSharp.RFWhisperer.Services
{
    public class ScannerService
    {
        // ── Events ───────────────────────────────────────────────────────────────
        /// <summary>Fired when a signal above threshold is detected at a frequency.</summary>
        public event Action<ScanResult>? SignalFound;
        /// <summary>Fired on every frequency step so the UI can show progress.</summary>
        public event Action<long>? StepChanged;
        /// <summary>Fired when a full sweep completes (Sweep/Monitor modes).</summary>
        public event Action? SweepComplete;

        // ── Configuration ────────────────────────────────────────────────────────
        public long StartFrequency   { get; set; } = 87_500_000;
        public long EndFrequency     { get; set; } = 108_000_000;
        public long StepHz           { get; set; } = 200_000;
        public int  DwellMs          { get; set; } = 400;
        public float SignalThresholdDb { get; set; } = 15f;  // min SNR to count as a hit
        public ScanMode Mode         { get; set; } = ScanMode.Seek;
        public bool WrapAround       { get; set; } = true;

        // ── State ────────────────────────────────────────────────────────────────
        public bool IsScanning => _cts != null && !_cts.IsCancellationRequested;
        public long CurrentFrequency { get; private set; }

        private readonly ISharpControl _sdr;
        private CancellationTokenSource? _cts;

        // ── Band presets ─────────────────────────────────────────────────────────
        public static readonly IReadOnlyList<ScanBand> Bands = new List<ScanBand>
        {
            new("FM Broadcast",   87_500_000,    108_000_000,  200_000,  400, "WFM",  200_000),
            new("AM Broadcast",      530_000,      1_710_000,   10_000,  500, "AM",    10_000),
            new("Aviation VHF",  118_000_000,    137_000_000,   25_000,  300, "AM",     8_000),
            new("Marine VHF",    156_000_000,    174_000_000,   25_000,  350, "WFM",   15_000),
            new("NOAA Weather",  162_400_000,    162_550_000,   25_000,  600, "WFM",   50_000),
            new("2m Amateur",    144_000_000,    148_000_000,   12_500,  400, "WFM",   12_500),
            new("70cm Amateur",  420_000_000,    450_000_000,   25_000,  300, "WFM",   12_500),
            new("FRS/GMRS",      462_550_000,    467_725_000,   25_000,  350, "WFM",   12_500),
            new("UHF Public Safety", 450_000_000, 470_000_000,  25_000,  350, "WFM",   12_500),
            new("ISM 433 MHz",   433_050_000,    434_790_000,   25_000,  300, "WFM",   25_000),
            new("Custom",                  0,              0,       0,  400, "",           0),
        };

        public ScannerService(ISharpControl sdr)
        {
            _sdr = sdr;
        }

        // ── Control ───────────────────────────────────────────────────────────────

        public void Start()
        {
            if (IsScanning) return;
            _cts = new CancellationTokenSource();
            _ = RunAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts = null;
        }

        public void ApplyBand(ScanBand band)
        {
            StartFrequency = band.StartHz;
            EndFrequency   = band.EndHz;
            StepHz         = band.StepHz;
            DwellMs        = band.DwellMs;

            if (!string.IsNullOrEmpty(band.Modulation) &&
                Enum.TryParse<DetectorType>(band.Modulation, ignoreCase: true, out var dt))
            {
                _sdr.DetectorType    = dt;
                _sdr.FilterBandwidth = band.BandwidthHz;
            }
        }

        // ── Scan loop ─────────────────────────────────────────────────────────────

        private async Task RunAsync(CancellationToken ct)
        {
            long freq = StartFrequency;

            do
            {
                while (freq <= EndFrequency && !ct.IsCancellationRequested)
                {
                    TuneToFrequency(freq);
                    StepChanged?.Invoke(freq);

                    // Wait for the radio to settle then sample
                    await Task.Delay(DwellMs, ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested) return;

                    float snr   = _sdr.VisualSNR;
                    float power = _sdr.VisualPeak;

                    if (snr >= SignalThresholdDb)
                    {
                        var result = new ScanResult(freq, power, snr, DateTime.Now);
                        SignalFound?.Invoke(result);

                        if (Mode == ScanMode.Seek)
                        {
                            // Wait for the signal to go quiet before resuming
                            await WaitForSilenceAsync(ct).ConfigureAwait(false);
                            if (ct.IsCancellationRequested) return;
                        }
                    }

                    freq += StepHz;
                }

                SweepComplete?.Invoke();

                // Monitor mode: loop back to start; Sweep mode: stop
                if (Mode == ScanMode.Monitor && WrapAround)
                    freq = StartFrequency;
                else
                    break;

            } while (!ct.IsCancellationRequested);

            _cts = null;
        }

        private void TuneToFrequency(long hz)
        {
            CurrentFrequency = hz;

            // If frequency is outside current display bandwidth, move center too
            long halfBw = _sdr.RFDisplayBandwidth / 2;
            if (Math.Abs(hz - _sdr.CenterFrequency) > halfBw * 0.8)
                _sdr.CenterFrequency = hz;

            _sdr.Frequency = hz;
        }

        /// <summary>
        /// In Seek mode: wait until SNR drops below threshold, then add a short
        /// tail delay so the scanner doesn't immediately re-trigger on the same signal.
        /// </summary>
        private async Task WaitForSilenceAsync(CancellationToken ct)
        {
            const int POLL_MS    = 200;
            const int TAIL_MS    = 800;   // quiet time before moving on
            int quietMs = 0;

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(POLL_MS, ct).ConfigureAwait(false);

                if (_sdr.VisualSNR < SignalThresholdDb)
                {
                    quietMs += POLL_MS;
                    if (quietMs >= TAIL_MS) return;
                }
                else
                {
                    quietMs = 0;  // signal came back, reset counter
                }
            }
        }
    }
}
