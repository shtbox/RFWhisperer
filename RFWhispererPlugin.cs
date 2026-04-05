using System.ComponentModel;
using System.Windows.Forms;
using SDRSharp.Common;
using SDRSharp.Radio;
using SDRSharp.RFWhisperer.Processors;
using SDRSharp.RFWhisperer.Services;
using SDRSharp.RFWhisperer.UI;

namespace SDRSharp.RFWhisperer
{
    /// <summary>
    /// SDR# RF Whisperer plugin entry point.
    /// Implements ISharpPlugin + ICanLazyLoadGui + IExtendedNameProvider
    /// to match the SDR# .NET 9 plugin SDK conventions.
    /// </summary>
    public class RFWhispererPlugin : ISharpPlugin, ICanLazyLoadGui, IExtendedNameProvider
    {
        // ── ISharpPlugin ─────────────────────────────────────────────────────────
        public string DisplayName => "RF Whisperer";

        // ── IExtendedNameProvider ────────────────────────────────────────────────
        public string Category    => "AI";
        public string MenuItemName => DisplayName;

        // ── ICanLazyLoadGui ──────────────────────────────────────────────────────
        public bool IsActive => _gui != null && _gui.Visible;

        public UserControl Gui
        {
            get
            {
                LoadGui();
                return _gui!;
            }
        }

        // ── Private state ────────────────────────────────────────────────────────
        private ISharpControl? _control;
        private UserControl? _gui;
        private RFWhispererPanel? _panel;
        private SignalProcessor? _signalProcessor;
        private SignalAnalyzer _analyzer = new();
        private System.Windows.Forms.Timer? _contextTimer;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        public void Initialize(ISharpControl control)
        {
            _control = control;
            _control.PropertyChanged += OnPropertyChanged;
        }

        public void LoadGui()
        {
            if (_gui != null) return;

            try
            {
                _panel = new RFWhispererPanel(_control!, _analyzer);
                _gui = _panel;

                // Register IQ stream hook after GUI is ready
                _signalProcessor = new SignalProcessor(_analyzer);
                _control!.RegisterStreamHook(_signalProcessor, ProcessorType.DecimatedAndFilteredIQ);

                // Poll every 500 ms to refresh the signal status bar
                _contextTimer = new System.Windows.Forms.Timer { Interval = 500 };
                _contextTimer.Tick += (_, _) => _panel?.RefreshSignalStatus();
                _contextTimer.Start();
            }
            catch (Exception ex)
            {
                _panel = null;
                _gui = BuildFallbackGui(ex);
            }
        }

        public void Close()
        {
            _contextTimer?.Stop();
            _contextTimer?.Dispose();

            if (_control != null)
            {
                _control.PropertyChanged -= OnPropertyChanged;
                if (_signalProcessor != null)
                    _control.UnregisterStreamHook(_signalProcessor);
            }

            _gui?.Dispose();
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            _panel?.OnSdrPropertyChanged(e.PropertyName ?? "");
        }

        private static UserControl BuildFallbackGui(Exception ex)
        {
            var host = new UserControl
            {
                BackColor = System.Drawing.Color.FromArgb(30, 30, 30)
            };

            var message = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = host.BackColor,
                ForeColor = System.Drawing.Color.Gainsboro,
                Text = "RF Whisperer failed to initialize.\r\n\r\n" +
                       "SDR# should still start normally.\r\n\r\n" +
                       ex.Message
            };

            host.Controls.Add(message);
            return host;
        }
    }
}
