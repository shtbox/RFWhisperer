namespace SDRSharp.RFWhisperer.Models
{
    public record ScanResult(
        long FrequencyHz,
        float SignalDbfs,
        float SnrDb,
        DateTime Timestamp
    )
    {
        public string FrequencyLabel =>
            FrequencyHz >= 1_000_000
                ? $"{FrequencyHz / 1_000_000.0:F4} MHz"
                : $"{FrequencyHz / 1_000.0:F3} kHz";
    }

    public enum ScanMode
    {
        Seek,    // Stop on each signal, resume when quiet
        Sweep,   // Log all hits and keep going
        Monitor  // Repeat range continuously
    }

    public record ScanBand(
        string Name,
        long StartHz,
        long EndHz,
        long StepHz,
        int DwellMs,
        string Modulation,
        int BandwidthHz
    );
}
