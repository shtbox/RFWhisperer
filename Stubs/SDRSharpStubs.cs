// SDRSharpStubs.cs
// Stub interfaces that mirror SDRSharp.Common and SDRSharp.Radio.
// Replace with references to the real DLLs from your SDR# installation.
// Namespace must match exactly: SDRSharp.Common

using System.ComponentModel;
using System.Windows.Forms;

namespace SDRSharp.Common
{
    public enum ProcessorType
    {
        RawIQ = 0,
        DecimatedAndFilteredIQ = 1,
        DemodulatorOutput = 2,
        FilteredIQ = 3
    }

    public enum DetectorType
    {
        AM = 0,
        FM = 1,
        WFM = 2,
        USB = 3,
        LSB = 4,
        DSB = 5,
        CW = 6,
        BPSK31 = 7,
        RAW = 8
    }

    public enum WindowType
    {
        None = 0,
        Hamming = 1,
        Hann = 2,
        BlackmanHarris = 3,
        Nuttall = 4,
        BH4 = 5,
        FlatTop = 6
    }

    /// <summary>
    /// Unsafe processor interface for receiving IQ or audio samples from SDR#.
    /// </summary>
    public interface IProcessor
    {
        bool Enabled { get; set; }
        unsafe void Process(Complex* buffer, int length);
    }

    /// <summary>
    /// Unsafe audio processor interface for receiving demodulated audio.
    /// </summary>
    public interface IAudioProcessor
    {
        bool Enabled { get; set; }
        unsafe void Process(float* buffer, int length);
    }

    /// <summary>
    /// Main SDR# control interface — mirrors the real ISharpControl.
    /// </summary>
    public interface ISharpControl
    {
        // --- Frequency ---
        long CenterFrequency { get; set; }
        long Frequency { get; set; }
        int FrequencyShift { get; set; }
        bool FrequencyShiftEnabled { get; set; }

        // --- Demodulation ---
        DetectorType DetectorType { get; set; }
        bool FmStereo { get; set; }
        bool SwapIq { get; set; }

        // --- Filter ---
        int FilterBandwidth { get; set; }
        int FilterOrder { get; set; }
        WindowType FilterType { get; set; }
        bool FilterAudio { get; set; }

        // --- Gain / AGC ---
        float AudioGain { get; set; }
        bool UseAgc { get; set; }
        int AgcThreshold { get; set; }
        int AgcDecay { get; set; }
        int AgcSlope { get; set; }
        bool AgcHang { get; set; }
        bool UnityGain { get; set; }

        // --- Squelch ---
        bool SquelchEnabled { get; set; }
        int SquelchThreshold { get; set; }

        // --- FFT (read-only) ---
        int FFTResolution { get; }
        int FFTRange { get; }
        int FFTOffset { get; }

        // --- State ---
        bool IsPlaying { get; }

        // --- Control ---
        void StartRadio();
        void StopRadio();
        void RegisterStreamHook(object processor, ProcessorType processorType);
        void UnregisterStreamHook(object processor);

        // --- Events ---
        event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Main plugin interface all SDR# plugins must implement.
    /// </summary>
    public interface ISharpPlugin
    {
        string DisplayName { get; }
        bool HasGui { get; }
        UserControl GuiControl { get; }
        void Initialize(ISharpControl control);
        void Close();
    }

    /// <summary>
    /// Complex sample struct (matches SDR# internal layout).
    /// </summary>
    public struct Complex
    {
        public float Real;
        public float Imaginary;

        public float Magnitude => MathF.Sqrt(Real * Real + Imaginary * Imaginary);
        public float MagnitudeSquared => Real * Real + Imaginary * Imaginary;
    }
}
