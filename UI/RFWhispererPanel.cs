using System.ComponentModel;
using System.Drawing;
using System.Text.Json.Nodes;
using System.Windows.Forms;
using SDRSharp.Common;
using SDRSharp.Radio;
using SDRSharp.RFWhisperer.Models;
using SDRSharp.RFWhisperer.Services;

namespace SDRSharp.RFWhisperer.UI
{
    public partial class RFWhispererPanel : UserControl
    {
        private readonly ISharpControl _sdr;
        private readonly SignalAnalyzer _analyzer;
        private readonly LLMService _llm;
        private readonly SignalContext _context = new();
        private CancellationTokenSource _cts = new();

        private static readonly Color BgDark = Color.FromArgb(30, 30, 30);
        private static readonly Color BgMedium = Color.FromArgb(45, 45, 48);
        private static readonly Color BgLight = Color.FromArgb(60, 60, 63);
        private static readonly Color AccentBlue = Color.FromArgb(0, 122, 204);
        private static readonly Color AccentGreen = Color.FromArgb(50, 180, 100);
        private static readonly Color TextMain = Color.FromArgb(220, 220, 220);
        private static readonly Color TextDim = Color.FromArgb(140, 140, 140);
        private static readonly Color UserMsgBg = Color.FromArgb(0, 84, 140);
        private static readonly Color AiMsgBg = Color.FromArgb(50, 50, 55);

        private readonly List<Control> _openAiOnlyControls = new();
        private ActivityMonitor _activityMonitor = null!;
        private FastSweepScanner _fastSweep = null!;
        private readonly object _activityLock = new();
        private ScannerService _scanner = null!;
        private readonly List<ScanResult> _scanHits = new();
        private bool _applyingResponsiveLayout;

        public RFWhispererPanel()
            : this(null!, new SignalAnalyzer(), initializeRuntimeServices: false)
        {
        }

        public RFWhispererPanel(ISharpControl sdr, SignalAnalyzer analyzer)
            : this(sdr, analyzer, initializeRuntimeServices: true)
        {
        }

        private RFWhispererPanel(ISharpControl sdr, SignalAnalyzer analyzer, bool initializeRuntimeServices)
        {
            _sdr = sdr;
            _analyzer = analyzer;
            _llm = new LLMService();
            _llm.OnToolCall = ExecuteToolCallAsync;
            InitializeComponent();
            ConfigureVisualStyle();
            HandleCreated += OnPanelHandleCreated;

            if (!initializeRuntimeServices || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            _scanner = new ScannerService(sdr);
            _activityMonitor = new ActivityMonitor(sdr);
            _fastSweep = new FastSweepScanner(sdr);

            LoadSettings();

            _scanner.SignalFound += OnScannerSignalFound;
            _scanner.StepChanged += OnScannerStep;
            _scanner.SweepComplete += OnScannerSweepComplete;

            _activityMonitor.ActivePeaksUpdated += OnActivityPeaksUpdated;
            _activityMonitor.TransmissionStarted += OnTransmissionStarted;

            _fastSweep.PeakFound += OnFastSweepPeak;
            _fastSweep.SweepComplete += OnFastSweepComplete;
            _fastSweep.CenterChanged += OnFastSweepCenter;
            ApplyResponsiveLayout();
            RefreshSignalStatus();
        }

        private void OnPanelHandleCreated(object? sender, EventArgs e) => ApplyDarkTitleBar();

        private void ConfigureVisualStyle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            ConfigureFlatTextHost(_chatInput);
            ConfigureFlatTextHost(_monThreshBox);
            ConfigureFlatTextHost(_fastSweepStartBox);
            ConfigureFlatTextHost(_fastSweepEndBox);
            ConfigureFlatTextHost(_scanStartBox);
            ConfigureFlatTextHost(_scanEndBox);
            ConfigureFlatTextHost(_scanStepBox);
            ConfigureFlatTextHost(_scanDwellBox);
            ConfigureFlatTextHost(_scanThreshBox);
            ConfigureFlatTextHost(_baseUrlBox);
            ConfigureFlatTextHost(_apiKeyBox);
            ConfigureFlatTextHost(_modelBox);
            ConfigureFlatTextHost(_timeoutBox);

            ConfigureFrequencyTextBox(_fastSweepStartBox);
            ConfigureFrequencyTextBox(_fastSweepEndBox);
            ConfigureFrequencyTextBox(_scanStartBox);
            ConfigureFrequencyTextBox(_scanEndBox);

            ConfigureFlatCombo(_providerCombo);
            ConfigureFlatCombo(_bandCombo);
            ConfigureFlatCombo(_scanModeCombo);

            _tabs.HandleCreated -= OnDarkThemeControlHandleCreated;
            _tabs.HandleCreated += OnDarkThemeControlHandleCreated;
            Resize -= OnResponsiveResize;
            Resize += OnResponsiveResize;
            _activityList.Resize -= OnResponsiveResize;
            _activityList.Resize += OnResponsiveResize;
        }

        private void OnResponsiveResize(object? sender, EventArgs e) => ApplyResponsiveLayout();

        private void ApplyResponsiveLayout()
        {
            if (_applyingResponsiveLayout || IsDisposed)
                return;

            _applyingResponsiveLayout = true;

            try
            {
            int availableWidth = Math.Max(260, ClientSize.Width);
            int tabWidth = Math.Max(52, Math.Min(78, (availableWidth - 12) / Math.Max(1, _tabs.TabPages.Count)));
            if (_tabs.ItemSize.Width != tabWidth || _tabs.ItemSize.Height != 22)
                _tabs.ItemSize = new Size(tabWidth, 22);

            int actionWidth = availableWidth < 320 ? 48 : 58;
            if (_sendBtn != null && _sendBtn.MinimumSize.Width != actionWidth)
                _sendBtn.MinimumSize = new Size(actionWidth, 0);
            if (_clearBtn != null && _clearBtn.MinimumSize.Width != actionWidth)
                _clearBtn.MinimumSize = new Size(actionWidth, 0);

            if (_activityList != null && _activityList.Columns.Count >= 4)
            {
                int width = Math.Max(180, _activityList.ClientSize.Width);
                int col0 = Math.Max(72, (int)(width * 0.34));
                int col1 = Math.Max(48, (int)(width * 0.18));
                int col2 = Math.Max(56, (int)(width * 0.22));
                int col3 = Math.Max(56, width - col0 - col1 - col2 - 4);

                if (_activityList.Columns[0].Width != col0) _activityList.Columns[0].Width = col0;
                if (_activityList.Columns[1].Width != col1) _activityList.Columns[1].Width = col1;
                if (_activityList.Columns[2].Width != col2) _activityList.Columns[2].Width = col2;
                if (_activityList.Columns[3].Width != col3) _activityList.Columns[3].Width = col3;
            }
            }
            finally
            {
                _applyingResponsiveLayout = false;
            }
        }

        private static void ConfigureFlatTextHost(TextBox? textBox)
        {
            if (textBox?.Parent is not Panel host)
                return;

            textBox.Multiline = false;
            textBox.AutoSize = false;
            textBox.Height = 22;
            host.BackColor = BgLight;
            host.Padding = new Padding(0);
            host.Margin = new Padding(0, 3, 0, 3);
            host.Height = 22;
            host.MinimumSize = new Size(0, 22);
            host.MaximumSize = new Size(10000, 22);
        }

        private void ConfigureFrequencyTextBox(TextBox? textBox)
        {
            if (textBox == null)
                return;

            textBox.Leave -= FrequencyTextBox_Leave;
            textBox.Leave += FrequencyTextBox_Leave;
            textBox.Text = FormatFrequencyTriplets(textBox.Text);
        }

        private void FrequencyTextBox_Leave(object? sender, EventArgs e)
        {
            if (sender is TextBox textBox)
                textBox.Text = FormatFrequencyTriplets(textBox.Text);
        }

        private static string FormatFrequencyTriplets(string text)
        {
            string digits = new(text.Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(digits))
                return text.Trim();

            var parts = new List<string>();
            for (int end = digits.Length; end > 0; end -= 3)
            {
                int start = Math.Max(0, end - 3);
                parts.Add(digits[start..end]);
            }
            parts.Reverse();
            return string.Join(".", parts);
        }

        private static bool TryParseFrequencyText(string text, out long value) =>
            long.TryParse(new string(text.Where(char.IsDigit).ToArray()), out value);

        private void ConfigureFlatCombo(ComboBox? combo)
        {
            if (combo == null)
                return;

            combo.FlatStyle = FlatStyle.Flat;
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.ItemHeight = Math.Max(combo.ItemHeight, 18);
            combo.IntegralHeight = false;
            combo.DrawItem -= DrawDarkComboItem;
            combo.DrawItem += DrawDarkComboItem;
            combo.HandleCreated -= OnDarkThemeControlHandleCreated;
            combo.HandleCreated += OnDarkThemeControlHandleCreated;
        }

        private void OnDarkThemeControlHandleCreated(object? sender, EventArgs e)
        {
            if (sender is Control control)
                ApplyDarkTheme(control);
        }

        private void DrawDarkComboItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not ComboBox combo || e.Index < 0)
                return;

            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color back = selected ? AccentBlue : BgLight;
            Color fore = TextMain;

            using var backBrush = new SolidBrush(back);
            using var foreBrush = new SolidBrush(fore);
            e.Graphics.FillRectangle(backBrush, e.Bounds);
            e.Graphics.DrawString(combo.Items[e.Index]?.ToString() ?? "", combo.Font, foreBrush, e.Bounds.X + 4, e.Bounds.Y + 1);
            e.DrawFocusRectangle();
        }
        private void MonStartBtn_Click(object? sender, EventArgs e)
        {
            if (_activityMonitor.IsRunning)
            {
                _activityMonitor.Stop();
                _monStartBtn.Text = "Start Monitor";
                _monStartBtn.BackColor = AccentGreen;
                _monStatusLabel.Text = "Stopped";
                return;
            }

            if (!float.TryParse(_monThreshBox.Text.Trim(), out float thresh))
                thresh = -70f;

            _activityMonitor.ThresholdDb = thresh;
            _activityMonitor.AutoJump = _autoJumpCheck.Checked;
            _activityMonitor.Start();

            _monStartBtn.Text = "Stop Monitor";
            _monStartBtn.BackColor = Color.FromArgb(180, 60, 60);
            _monStatusLabel.Text = _autoJumpCheck.Checked
                ? "Running — auto-jumping on new transmissions"
                : "Running — watching current view";
        }

        private void OnActivityPeaksUpdated(IReadOnlyList<SpectrumReader.Peak> peaks)
        {
            if (InvokeRequired) { BeginInvoke(() => OnActivityPeaksUpdated(peaks)); return; }

            _activityList.BeginUpdate();
            var existing = new Dictionary<string, ListViewItem>();
            foreach (ListViewItem item in _activityList.Items)
                existing[item.Text] = item;

            var seenLabels = new HashSet<string>();
            foreach (var peak in peaks)
            {
                string label = peak.Label;
                seenLabels.Add(label);

                string powerStr = $"{peak.PowerDb:F1} dBfs";
                string timeStr = DateTime.Now.ToString("HH:mm:ss");

                if (existing.TryGetValue(label, out var item))
                {
                    item.SubItems[1].Text = powerStr;
                    item.SubItems[2].Text = "Active";
                    item.SubItems[3].Text = timeStr;
                    item.ForeColor = AccentGreen;
                }
                else
                {
                    var newItem = new ListViewItem(label);
                    newItem.SubItems.Add(powerStr);
                    newItem.SubItems.Add("Active");
                    newItem.SubItems.Add(timeStr);
                    newItem.ForeColor = AccentGreen;
                    _activityList.Items.Add(newItem);
                }
            }

            foreach (ListViewItem item in _activityList.Items)
            {
                if (!seenLabels.Contains(item.Text) && item.SubItems[2].Text == "Active")
                {
                    item.SubItems[2].Text = "Gone";
                    item.ForeColor = TextDim;
                }
            }

            _activityList.EndUpdate();
        }

        private void OnTransmissionStarted(SpectrumReader.Peak peak)
        {
            if (InvokeRequired) { BeginInvoke(() => OnTransmissionStarted(peak)); return; }
            _monStatusLabel.Text = $"TX: {peak.Label}  {peak.PowerDb:F0} dBfs  {DateTime.Now:HH:mm:ss}";
            _monStatusLabel.ForeColor = AccentGreen;
        }

        private void ActivityList_DoubleClick(object? sender, EventArgs e) => TuneToSelectedActivity();

        private void TuneToSelectedActivity()
        {
            if (_activityList.SelectedItems.Count == 0) return;
            string label = _activityList.SelectedItems[0].Text;
            var peak = _activityMonitor.CurrentPeaks.FirstOrDefault(p => p.Label == label);
            if (peak == null) return;
            _sdr.Frequency = peak.FrequencyHz;
            _sdr.CenterFrequency = peak.FrequencyHz;
        }

        private async void ActivityAskAi_Click(object? sender, EventArgs e)
        {
            if (_activityList.SelectedItems.Count == 0) return;
            string label = _activityList.SelectedItems[0].Text;
            var peak = _activityMonitor.CurrentPeaks.FirstOrDefault(p => p.Label == label);
            if (peak == null) return;

            _sdr.Frequency = peak.FrequencyHz;
            _sdr.CenterFrequency = peak.FrequencyHz;
            _tabs.SelectedIndex = 0;

            await SendPromptAsync(
                $"I can see an active signal at {peak.Label} ({peak.PowerDb:F0} dBfs). " +
                "What is this? Identify it and apply the best settings.");
        }

        private void FastSweepBtn_Click(object? sender, EventArgs e)
        {
            if (_fastSweep.IsRunning)
            {
                _fastSweep.Stop();
                _fastSweepBtn.Text = "Fast Sweep";
                _fastSweepBtn.BackColor = AccentBlue;
                _fastSweepStatusLabel.Text = "Stopped.";
                return;
            }

            if (!TryParseFrequencyText(_fastSweepStartBox.Text.Trim(), out long start)) { _fastSweepStatusLabel.Text = "Invalid start."; return; }
            if (!TryParseFrequencyText(_fastSweepEndBox.Text.Trim(), out long end)) { _fastSweepStatusLabel.Text = "Invalid end."; return; }
            if (end <= start) { _fastSweepStatusLabel.Text = "End must be > start."; return; }

            _fastSweep.StartHz = start;
            _fastSweep.EndHz = end;
            _fastSweep.RepeatSweep = _fastSweepRepeatCheck.Checked;
            _fastSweep.ThresholdDb = float.TryParse(_monThreshBox.Text.Trim(), out float t) ? t : -70f;

            _activityList.Items.Clear();
            _fastSweepBtn.Text = "Stop Sweep";
            _fastSweepBtn.BackColor = Color.FromArgb(180, 60, 60);
            _fastSweepStatusLabel.Text = "Sweeping...";
            _fastSweep.Start();
        }

        private void OnFastSweepCenter(long center)
        {
            if (InvokeRequired) { BeginInvoke(() => OnFastSweepCenter(center)); return; }
            string label = center >= 1_000_000 ? $"{center / 1_000_000.0:F2} MHz" : $"{center / 1_000.0:F1} kHz";
            _fastSweepStatusLabel.Text = $"Center: {label}";
        }

        private void OnFastSweepPeak(ScanResult result)
        {
            if (InvokeRequired) { BeginInvoke(() => OnFastSweepPeak(result)); return; }

            string freq = result.FrequencyLabel;
            foreach (ListViewItem existing in _activityList.Items)
                if (existing.Text == freq) return;

            var item = new ListViewItem(freq);
            item.SubItems.Add($"{result.SignalDbfs:F1} dBfs");
            item.SubItems.Add("Found");
            item.SubItems.Add(result.Timestamp.ToString("HH:mm:ss"));
            item.ForeColor = AccentBlue;
            _activityList.Items.Add(item);
        }

        private void OnFastSweepComplete(IReadOnlyList<ScanResult> results)
        {
            if (InvokeRequired) { BeginInvoke(() => OnFastSweepComplete(results)); return; }

            if (!_fastSweep.RepeatSweep)
            {
                _fastSweepBtn.Text = "Fast Sweep";
                _fastSweepBtn.BackColor = AccentBlue;
            }
            _fastSweepStatusLabel.Text = $"Done — {results.Count} signal(s)";
        }

        private static TextBox ScanInput(string defaultValue) => new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(55, 55, 58),
            ForeColor = Color.FromArgb(220, 220, 220),
            BorderStyle = BorderStyle.None,
            Text = defaultValue,
            Margin = new Padding(0, 3, 0, 3)
        };

        private void BandCombo_Changed(object? sender, EventArgs e)
        {
            int idx = _bandCombo.SelectedIndex;
            if (idx < 0 || idx >= ScannerService.Bands.Count) return;
            var band = ScannerService.Bands[idx];
            if (band.Name == "Custom") return;

            _scanStartBox.Text = FormatFrequencyTriplets(band.StartHz.ToString());
            _scanEndBox.Text = FormatFrequencyTriplets(band.EndHz.ToString());
            _scanStepBox.Text = band.StepHz.ToString();
            _scanDwellBox.Text = band.DwellMs.ToString();
        }

        private void ScanStartBtn_Click(object? sender, EventArgs e)
        {
            if (_scanner.IsScanning)
            {
                _scanner.Stop();
                _scanStartBtn.Text = "Start Scan";
                _scanStartBtn.BackColor = AccentGreen;
                _scanStatusLabel.Text = "Stopped";
                _scanProgress.Value = 0;
                _scanFreqLabel.Text = "—";
                return;
            }

            if (!TryParseFrequencyText(_scanStartBox.Text.Trim(), out long start) || start <= 0) { ShowScanError("Invalid start frequency."); return; }
            if (!TryParseFrequencyText(_scanEndBox.Text.Trim(), out long end) || end <= start) { ShowScanError("End must be > start."); return; }
            if (!long.TryParse(_scanStepBox.Text.Trim(), out long step) || step <= 0) { ShowScanError("Invalid step."); return; }
            if (!int.TryParse(_scanDwellBox.Text.Trim(), out int dwell) || dwell < 50) { ShowScanError("Dwell must be ≥ 50 ms."); return; }
            if (!float.TryParse(_scanThreshBox.Text.Trim(), out float thresh)) { ShowScanError("Invalid SNR threshold."); return; }

            _scanner.StartFrequency = start;
            _scanner.EndFrequency = end;
            _scanner.StepHz = step;
            _scanner.DwellMs = dwell;
            _scanner.SignalThresholdDb = thresh;
            _scanner.Mode = _scanModeCombo.SelectedIndex switch
            {
                1 => ScanMode.Sweep,
                2 => ScanMode.Monitor,
                _ => ScanMode.Seek
            };

            int bandIdx = _bandCombo.SelectedIndex;
            if (bandIdx >= 0 && bandIdx < ScannerService.Bands.Count)
            {
                var band = ScannerService.Bands[bandIdx];
                if (band.Name != "Custom")
                    _scanner.ApplyBand(band);
            }

            _scanHits.Clear();
            _scanHitList.Items.Clear();
            _scanProgress.Value = 0;
            _scanStartBtn.Text = "Stop Scan";
            _scanStartBtn.BackColor = Color.FromArgb(180, 60, 60);
            _scanStatusLabel.Text = "Scanning...";

            _scanner.Start();
        }

        private void ShowScanError(string msg)
        {
            _scanFreqLabel.Text = msg;
            _scanFreqLabel.ForeColor = Color.OrangeRed;
        }

        private void OnScannerSignalFound(ScanResult result)
        {
            if (InvokeRequired) { BeginInvoke(() => OnScannerSignalFound(result)); return; }

            _scanHits.Add(result);
            string entry = $"{result.FrequencyLabel,-14}  SNR {result.SnrDb:F0} dB  {result.Timestamp:HH:mm:ss}";
            _scanHitList.Items.Add(entry);
            _scanHitList.SelectedIndex = _scanHitList.Items.Count - 1;

            _scanFreqLabel.Text = $"HIT: {result.FrequencyLabel}  —  SNR {result.SnrDb:F0} dB";
            _scanFreqLabel.ForeColor = AccentGreen;
        }

        private void OnScannerStep(long frequency)
        {
            if (InvokeRequired) { BeginInvoke(() => OnScannerStep(frequency)); return; }

            string label = frequency >= 1_000_000
                ? $"{frequency / 1_000_000.0:F4} MHz"
                : $"{frequency / 1_000.0:F3} kHz";

            _scanFreqLabel.Text = $"Scanning: {label}";
            _scanFreqLabel.ForeColor = AccentBlue;

            long range = _scanner.EndFrequency - _scanner.StartFrequency;
            if (range > 0)
            {
                long pos = frequency - _scanner.StartFrequency;
                _scanProgress.Value = (int)Math.Min(100, pos * 100 / range);
            }
        }

        private void OnScannerSweepComplete()
        {
            if (InvokeRequired) { BeginInvoke(OnScannerSweepComplete); return; }

            if (_scanner.Mode != ScanMode.Monitor)
            {
                _scanStartBtn.Text = "Start Scan";
                _scanStartBtn.BackColor = AccentGreen;
                _scanStatusLabel.Text = $"Done — {_scanHits.Count} signal(s) found";
                _scanProgress.Value = 100;
            }
        }

        private void ScanHitList_DoubleClick(object? sender, EventArgs e)
        {
            int idx = _scanHitList.SelectedIndex;
            if (idx < 0 || idx >= _scanHits.Count) return;
            var hit = _scanHits[idx];
            _sdr.Frequency = hit.FrequencyHz;
            _sdr.CenterFrequency = hit.FrequencyHz;
        }

        private async void ScanAskAi_Click(object? sender, EventArgs e)
        {
            int idx = _scanHitList.SelectedIndex;
            if (idx < 0 || idx >= _scanHits.Count) return;
            var hit = _scanHits[idx];

            _sdr.Frequency = hit.FrequencyHz;
            _sdr.CenterFrequency = hit.FrequencyHz;

            _tabs.SelectedIndex = 0;
            await SendPromptAsync(
                $"I found a signal at {hit.FrequencyLabel} with SNR {hit.SnrDb:F0} dB. " +
                "What is this signal? Identify it and apply the best settings to receive it.");
        }

        private static Panel Bordered(Control inner)
        {
            var border = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgLight,
                Padding = new Padding(0),
                Margin = new Padding(0, 3, 0, 3)
            };
            inner.Dock = DockStyle.Fill;
            border.Controls.Add(inner);
            return border;
        }

        private void AddPresetBtn(FlowLayoutPanel panel, string label, string url)
        {
            var btn = DarkButton(label, BgLight);
            btn.ForeColor = AccentBlue;
            btn.AutoSize = false;
            btn.Size = new Size(110, 30);
            btn.Padding = new Padding(6, 2, 6, 2);
            btn.Margin = new Padding(0, 0, 8, 8);
            btn.Click += (_, _) => _baseUrlBox.Text = url;
            panel.Controls.Add(btn);
        }

        private void UpdateProviderVisibility()
        {
            bool isOpenAI = _providerCombo?.SelectedIndex == 1;

            foreach (var c in _openAiOnlyControls)
                c.Visible = isOpenAI;

            if (_apiKeyBox != null)
                _apiKeyBox.PlaceholderText = isOpenAI
                    ? "API key  (leave blank for local models)"
                    : "sk-ant-...";

            if (_modelBox != null)
                _modelBox.PlaceholderText = isOpenAI
                    ? "llama3 / mistral / gpt-4o"
                    : "claude-opus-4-6 / claude-sonnet-4-6";
        }

        public void RefreshSignalStatus()
        {
            if (InvokeRequired) { Invoke(RefreshSignalStatus); return; }

            SyncContextFromSdr();

            string freq = _context.TuneFrequency >= 1_000_000
                ? $"{_context.TuneFrequency / 1_000_000.0:F3} MHz"
                : $"{_context.TuneFrequency / 1_000.0:F1} kHz";

            _freqLabel.Text = freq;
            _modeLabel.Text = _context.DetectorType;

            double snr = _context.SnrDb;
            _snrLabel.Text = $"SNR: {snr:F0} dB";
            _snrLabel.ForeColor = snr > 20 ? AccentGreen : snr > 10 ? Color.Orange : Color.OrangeRed;

            _carrierLabel.Text = "●";
            _carrierLabel.ForeColor = _context.CarrierDetected ? AccentGreen : Color.Gray;
            _carrierLabel.BackColor = Color.Transparent;
        }

        public void OnSdrPropertyChanged(string propertyName)
        {
            if (InvokeRequired)
                BeginInvoke(() => RefreshSignalStatus());
            else
                RefreshSignalStatus();
        }

        private void SyncContextFromSdr()
        {
            try
            {
                _context.CenterFrequency = _sdr.CenterFrequency;
                _context.TuneFrequency = _sdr.Frequency;
                _context.FilterBandwidth = _sdr.FilterBandwidth;
                _context.DetectorType = _sdr.DetectorType.ToString();
                _context.AudioGain = (int)Math.Round((double)_sdr.AudioGain);
                _context.UseAgc = _sdr.UseAgc;
                _context.IsPlaying = _sdr.IsPlaying;
                _context.SquelchEnabled = _sdr.SquelchEnabled;
                _context.SquelchThreshold = _sdr.SquelchThreshold;
                _context.SignalPowerDbfs = _sdr.VisualPeak;
                _context.NoiseFloorDbfs = _sdr.VisualFloor;
                _context.SnrDb = _sdr.VisualSNR;
                _context.CarrierDetected = _sdr.VisualSNR > 12f;
                _analyzer.UpdateModulationContext(_context);
            }
            catch { }
        }

        private async void SendBtn_Click(object? sender, EventArgs e) => await SendChatMessageAsync();

        private async void ChatInput_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                await SendChatMessageAsync();
            }
        }

        private async Task SendChatMessageAsync()
        {
            string text = _chatInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            _chatInput.Clear();
            await SendPromptAsync(text);
        }

        private async void SendQuickPrompt(string prompt) => await SendPromptAsync(prompt);

        private async Task SendPromptAsync(string prompt)
        {
            if (!_llm.IsConfigured)
            {
                AppendAiMessage("⚠️ Please enter your Anthropic API key in the Settings tab.");
                _tabs.SelectedIndex = 2;
                return;
            }

            AppendUserMessage(prompt);
            SetStatus("Thinking...");
            _sendBtn.Enabled = false;

            SyncContextFromSdr();
            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));

            try
            {
                string response = await _llm.SendMessageAsync(prompt, _context, _cts.Token);
                AppendAiMessage(response);
            }
            catch (OperationCanceledException)
            {
                AppendSystemMessage("Cancelled.");
            }
            catch (Exception ex)
            {
                AppendSystemMessage($"Error: {ex.Message}");
            }
            finally
            {
                SetStatus("Ready");
                _sendBtn.Enabled = true;
            }
        }

        private async void RunDiagBtn_Click(object? sender, EventArgs e)
        {
            if (!_llm.IsConfigured)
            {
                MessageBox.Show("Please enter your Anthropic API key in the Settings tab.",
                    "API Key Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _runDiagBtn.Enabled = false;
            _diagReport.Clear();
            _diagReport.AppendText("Running AI diagnostic...\n\n");

            SyncContextFromSdr();
            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));

            try
            {
                string report = await _llm.SendMessageAsync(
                    "Perform a full diagnostic of the current signal. Report: " +
                    "1) Signal quality assessment, 2) Whether the settings are optimal, " +
                    "3) Any issues detected, 4) Specific recommendations with settings changes applied automatically.",
                    _context, _cts.Token);

                _diagReport.Clear();
                _diagReport.AppendText(report);
                AppendAiMessage("[Diagnostic]\n" + report);
            }
            catch (Exception ex)
            {
                _diagReport.AppendText($"\nError: {ex.Message}");
            }
            finally
            {
                _runDiagBtn.Enabled = true;
            }
        }

        private async Task<string> ExecuteToolCallAsync(string toolName, JsonObject input)
        {
            string result = "";
            await Task.Run(() =>
            {
                if (InvokeRequired)
                    Invoke(() => result = ExecuteTool(toolName, input));
                else
                    result = ExecuteTool(toolName, input);
            });
            return result;
        }

        private string ExecuteTool(string toolName, JsonObject input)
        {
            try
            {
                switch (toolName)
                {
                    case "set_frequency":
                    {
                        long hz = input["frequency_hz"]?.GetValue<long>() ?? 0;
                        if (hz <= 0) return "Error: invalid frequency";
                        bool setCenter = input["set_center"]?.GetValue<bool>() ?? true;
                        _sdr.Frequency = hz;
                        if (setCenter) _sdr.CenterFrequency = hz;
                        RefreshSignalStatus();
                        return $"Tuned to {hz / 1_000_000.0:F4} MHz.";
                    }
                    case "set_modulation":
                    {
                        string mode = input["mode"]?.GetValue<string>() ?? "WFM";
                        if (Enum.TryParse<DetectorType>(mode, true, out var dt))
                        {
                            _sdr.DetectorType = dt;
                            RefreshSignalStatus();
                            return $"Modulation set to {mode}.";
                        }
                        return $"Unknown modulation mode: {mode}";
                    }
                    case "set_filter_bandwidth":
                    {
                        int bw = input["bandwidth_hz"]?.GetValue<int>() ?? 0;
                        if (bw <= 0) return "Error: invalid bandwidth";
                        _sdr.FilterBandwidth = bw;
                        return $"Filter bandwidth set to {bw / 1000.0:F1} kHz.";
                    }
                    case "set_audio_gain":
                    {
                        int gain = (int)(input["gain_db"]?.GetValue<double>() ?? 20);
                        _sdr.AudioGain = Math.Clamp(gain, 0, 40);
                        return $"Audio gain set to {_sdr.AudioGain} dB.";
                    }
                    case "set_agc":
                    {
                        bool enabled = input["enabled"]?.GetValue<bool>() ?? true;
                        _sdr.UseAgc = enabled;
                        if (input.ContainsKey("threshold_dbfs"))
                            _sdr.AgcThreshold = input["threshold_dbfs"]?.GetValue<int>() ?? -40;
                        return $"AGC {(enabled ? "enabled" : "disabled")}.";
                    }
                    case "set_squelch":
                    {
                        bool enabled = input["enabled"]?.GetValue<bool>() ?? false;
                        int threshold = input["threshold_dbfs"]?.GetValue<int>() ?? -40;
                        _sdr.SquelchEnabled = enabled;
                        _sdr.SquelchThreshold = threshold;
                        return $"Squelch {(enabled ? $"enabled at {threshold} dBfs" : "disabled")}.";
                    }
                    case "get_signal_info":
                        SyncContextFromSdr();
                        return _context.ToContextString();
                    case "apply_preset":
                        return ApplyPreset(input["preset"]?.GetValue<string>() ?? "");
                    case "start_radio":
                        _sdr.StartRadio();
                        return "Radio started.";
                    case "stop_radio":
                        _sdr.StopRadio();
                        return "Radio stopped.";
                    default:
                        return $"Unknown tool: {toolName}";
                }
            }
            catch (Exception ex)
            {
                return $"Error executing {toolName}: {ex.Message}";
            }
        }

        private string ApplyPreset(string preset)
        {
            switch (preset)
            {
                case "fm_broadcast":
                    _sdr.DetectorType = DetectorType.WFM;
                    _sdr.FilterBandwidth = 200_000;
                    _sdr.UseAgc = true;
                    _sdr.AudioGain = 20;
                    return "Applied FM Broadcast preset: WFM, 200 kHz, AGC on.";
                case "am_broadcast":
                    _sdr.DetectorType = DetectorType.AM;
                    _sdr.FilterBandwidth = 10_000;
                    _sdr.UseAgc = true;
                    _sdr.AudioGain = 25;
                    return "Applied AM Broadcast preset: AM, 10 kHz, AGC on.";
                case "aviation_am":
                    _sdr.DetectorType = DetectorType.AM;
                    _sdr.FilterBandwidth = 8_000;
                    _sdr.UseAgc = true;
                    _sdr.AudioGain = 30;
                    return "Applied Aviation preset: AM, 8 kHz BW, AGC on.";
                case "marine_vhf":
                    _sdr.DetectorType = DetectorType.WFM;
                    _sdr.FilterBandwidth = 15_000;
                    _sdr.UseAgc = true;
                    _sdr.SquelchEnabled = true;
                    _sdr.SquelchThreshold = -40;
                    return "Applied Marine VHF preset: NFM 15 kHz, AGC, squelch enabled.";
                case "noaa_weather":
                    _sdr.Frequency = 162_400_000;
                    _sdr.DetectorType = DetectorType.WFM;
                    _sdr.FilterBandwidth = 50_000;
                    _sdr.UseAgc = true;
                    return "Applied NOAA Weather preset: 162.4 MHz, WFM, 50 kHz BW.";
                case "amateur_ssb_hf":
                    _sdr.DetectorType = DetectorType.USB;
                    _sdr.FilterBandwidth = 3_000;
                    _sdr.UseAgc = true;
                    _sdr.AgcThreshold = -50;
                    return "Applied Amateur SSB/HF preset: USB, 3 kHz BW, AGC on.";
                case "amateur_fm_vhf":
                    _sdr.DetectorType = DetectorType.WFM;
                    _sdr.FilterBandwidth = 12_500;
                    _sdr.UseAgc = true;
                    _sdr.SquelchEnabled = true;
                    _sdr.SquelchThreshold = -45;
                    return "Applied Amateur FM/VHF preset: NFM 12.5 kHz, squelch enabled.";
                case "ads_b_1090":
                    _sdr.Frequency = 1_090_000_000;
                    _sdr.CenterFrequency = 1_090_000_000;
                    _sdr.DetectorType = DetectorType.RAW;
                    _sdr.FilterBandwidth = 2_000_000;
                    return "Tuned to ADS-B 1090 MHz. Use dump1090 or ADS-B plugin for decoding.";
                case "shortwave_am":
                    _sdr.DetectorType = DetectorType.AM;
                    _sdr.FilterBandwidth = 8_000;
                    _sdr.UseAgc = true;
                    return "Applied Shortwave AM preset: AM, 8 kHz BW, AGC on.";
                default:
                    return $"Unknown preset: {preset}";
            }
        }

        private void AppendUserMessage(string text)
        {
            if (InvokeRequired) { Invoke(() => AppendUserMessage(text)); return; }
            AppendChatNewline(BgDark);
            AppendChatRun("You  ", new Font("Segoe UI", 8.5f, FontStyle.Bold), AccentBlue, UserMsgBg);
            AppendChatNewline(UserMsgBg);
            AppendChatRun(text, new Font("Segoe UI", 10f), TextMain, UserMsgBg);
            AppendChatNewline(UserMsgBg);
            AppendChatNewline(BgDark);
            _chatHistory.ScrollToCaret();
        }

        private void AppendAiMessage(string text)
        {
            if (InvokeRequired) { Invoke(() => AppendAiMessage(text)); return; }
            AppendChatNewline(BgDark);
            AppendChatRun("AI  ", new Font("Segoe UI", 8.5f, FontStyle.Bold), AccentGreen, AiMsgBg);
            AppendChatNewline(AiMsgBg);
            MarkdownRenderer.Append(_chatHistory, text, TextMain, AiMsgBg);
            AppendChatNewline(BgDark);
            _chatHistory.ScrollToCaret();
        }

        private void AppendSystemMessage(string text)
        {
            if (InvokeRequired) { Invoke(() => AppendSystemMessage(text)); return; }
            AppendChatNewline(BgDark);
            AppendChatRun($"  {text}  ", new Font("Segoe UI", 8.5f, FontStyle.Italic), TextDim, BgDark);
            AppendChatNewline(BgDark);
            _chatHistory.ScrollToCaret();
        }

        private void AppendChatRun(string text, Font font, Color fg, Color bg)
        {
            int start = _chatHistory.TextLength;
            _chatHistory.AppendText(text);
            _chatHistory.Select(start, text.Length);
            _chatHistory.SelectionFont = font;
            _chatHistory.SelectionColor = fg;
            _chatHistory.SelectionBackColor = bg;
            _chatHistory.SelectionLength = 0;
        }

        private void AppendChatNewline(Color bg)
        {
            int start = _chatHistory.TextLength;
            _chatHistory.AppendText("\n");
            _chatHistory.Select(start, 1);
            _chatHistory.SelectionBackColor = bg;
            _chatHistory.SelectionLength = 0;
        }

        private void SetStatus(string text)
        {
            if (InvokeRequired) { Invoke(() => SetStatus(text)); return; }
            _statusLabel.Text = text;
        }

        private void SaveSettings_Click(object? sender, EventArgs e)
        {
            SaveSettings();
            _settingsStatus.ForeColor = AccentGreen;
            _settingsStatus.Text = "Settings saved.";
            Task.Delay(2000).ContinueWith(_ => Invoke(() => _settingsStatus.Text = ""));
        }

        private async void TestBtn_Click(object? sender, EventArgs e)
        {
            SaveSettings();
            _testBtn.Enabled = false;
            _settingsStatus.ForeColor = TextDim;
            _settingsStatus.Text = "Testing connection...";

            try
            {
                var ctx = new SignalContext();
                string reply = await _llm.SendMessageAsync(
                    "Respond with exactly one word: CONNECTED", ctx,
                    new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token);

                _settingsStatus.ForeColor = AccentGreen;
                _settingsStatus.Text = $"Connected  —  {reply.Trim().Split('\n')[0].Truncate(40)}";
            }
            catch (Exception ex)
            {
                _settingsStatus.ForeColor = Color.OrangeRed;
                _settingsStatus.Text = ex.Message.Truncate(60);
            }
            finally
            {
                _testBtn.Enabled = true;
            }
        }

        private int TimeoutSeconds =>
            int.TryParse(_timeoutBox?.Text, out int v) && v > 0 ? v : 120;

        private void SaveSettings()
        {
            var providerType = _providerCombo.SelectedIndex == 1
                ? ProviderType.OpenAICompatible
                : ProviderType.Anthropic;

            var data = new PluginSettingsData(
                PluginSettings.SerializeProviderType(providerType),
                _apiKeyBox.Text.Trim(),
                _baseUrlBox.Text.Trim(),
                _modelBox.Text.Trim(),
                _beginnerCheck.Checked,
                TimeoutSeconds);

            _llm.Configure(providerType, data.ApiKey, data.BaseUrl, data.Model, data.BeginnerMode);
            PluginSettings.Save(data);
        }

        private void LoadSettings()
        {
            var s = PluginSettings.Load();
            var providerType = PluginSettings.ParseProviderType(s.ProviderType);

            if (_providerCombo != null)
                _providerCombo.SelectedIndex = providerType == ProviderType.OpenAICompatible ? 1 : 0;
            if (_baseUrlBox != null) _baseUrlBox.Text = s.BaseUrl;
            if (_apiKeyBox != null) _apiKeyBox.Text = s.ApiKey;
            if (_modelBox != null) _modelBox.Text = s.Model;
            if (_timeoutBox != null) _timeoutBox.Text = s.TimeoutSeconds.ToString();
            if (_beginnerCheck != null) _beginnerCheck.Checked = s.BeginnerMode;

            _llm.Configure(providerType, s.ApiKey, s.BaseUrl, s.Model, s.BeginnerMode);
            UpdateProviderVisibility();
        }

        private static Button DarkButton(string text, Color back) =>
            new()
            {
                Text = text,
                BackColor = back,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Font = new Font("Segoe UI", 8.5f)
            };

        private static Label StatusLabel(string text, Color color, bool bold = false) =>
            new()
            {
                Text = text,
                ForeColor = color,
                BackColor = Color.Transparent,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", bold ? 9.5f : 8.5f, bold ? FontStyle.Bold : FontStyle.Regular)
            };

        private static Label SettingsLabel(string text) =>
            new()
            {
                Text = text,
                ForeColor = TextDim,
                BackColor = Color.Transparent,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 6, 0)
            };

        private void AddQuickButton(FlowLayoutPanel panel, string label, string prompt)
        {
            var btn = DarkButton(label, BgLight);
            btn.ForeColor = AccentBlue;
            btn.AutoSize = true;
            btn.Padding = new Padding(4, 2, 4, 2);
            btn.Click += (_, _) => SendQuickPrompt(prompt);
            panel.Controls.Add(btn);
        }

        private void DrawTabItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tc) return;
            var page = tc.TabPages[e.Index];
            bool selected = (e.State & DrawItemState.Selected) != 0;

            using var bgBrush = new SolidBrush(selected ? BgMedium : BgDark);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            if (selected)
            {
                using var accentBrush = new SolidBrush(AccentBlue);
                e.Graphics.FillRectangle(accentBrush,
                    new Rectangle(e.Bounds.X, e.Bounds.Bottom - 2, e.Bounds.Width, 2));
            }

            using var textBrush = new SolidBrush(selected ? TextMain : TextDim);
            var tabFont = new Font("Segoe UI", 8f, selected ? FontStyle.Bold : FontStyle.Regular);
            var fmt = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            e.Graphics.DrawString(page.Text, tabFont, textBrush, e.Bounds, fmt);
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private void ApplyDarkTitleBar()
        {
            try
            {
                Control? parent = this;
                while (parent?.Parent != null) parent = parent.Parent;
                if (parent == null || parent.Handle == IntPtr.Zero) return;

                int dark = 1;
                DwmSetWindowAttribute(parent.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
                ApplyDarkTheme(parent);
                ApplyDarkTheme(_tabs);
                ApplyDarkTheme(_providerCombo);
                ApplyDarkTheme(_bandCombo);
                ApplyDarkTheme(_scanModeCombo);
                ApplyDarkTheme(_activityList);
                ApplyDarkTheme(_scanHitList);
            }
            catch { }
        }

        private static void ApplyDarkTheme(Control? control)
        {
            if (control == null || control.IsDisposed || control.Handle == IntPtr.Zero)
                return;

            try
            {
                SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
                _scanner?.Stop();
                _activityMonitor?.Dispose();
                _fastSweep?.Dispose();
                _llm.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal static class StringExtensions
    {
        public static string Truncate(this string s, int max) =>
            s.Length <= max ? s : s[..max] + "…";
    }
}
