using System.Drawing;
using System.Windows.Forms;

namespace SDRSharp.RFWhisperer.UI
{
    partial class RFWhispererPanel
    {
        private void InitializeComponent()
        {
            BackColor = BgDark;
            ForeColor = TextMain;
            MinimumSize = new Size(340, 500);
            Size = new Size(380, 600);
            Font = new Font("Segoe UI", 9f);

            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                BackColor = BgDark,
                ForeColor = TextMain,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(64, 22),
                Font = new Font("Segoe UI", 8f)
            };
            _tabs.DrawItem += DrawTabItem;

            _tabs.TabPages.Add(BuildChatTab());
            _tabs.TabPages.Add(BuildDiagnosticsTab());
            _tabs.TabPages.Add(BuildMonitorTab());
            _tabs.TabPages.Add(BuildScannerTab());
            _tabs.TabPages.Add(BuildSettingsTab());

            Controls.Add(_tabs);

        }

        // ── Chat Tab ─────────────────────────────────────────────────────────────

        private TabPage BuildChatTab()
        {
            var page = new TabPage("Chat") { BackColor = BgDark, ForeColor = TextMain };

            // Signal status bar
            var statusBar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = BgMedium,
                Padding = new Padding(6, 4, 6, 4),
                ColumnCount = 4,
                RowCount = 2
            };
            statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));

            _freqLabel    = StatusLabel("-- MHz", TextMain, bold: true);
            _modeLabel    = StatusLabel("--", TextDim);
            _snrLabel     = StatusLabel("SNR: --", TextDim);
            _carrierLabel = StatusLabel("●", Color.Gray);

            statusBar.Controls.Add(_freqLabel,    0, 0);
            statusBar.Controls.Add(_modeLabel,    1, 0);
            statusBar.Controls.Add(_snrLabel,     2, 0);
            statusBar.Controls.Add(_carrierLabel, 3, 0);

            // Quick prompt buttons
            var quickBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = BgDark,
                Padding = new Padding(4, 4, 4, 0),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            AddQuickButton(quickBar, "Identify",      "What signal am I receiving? Identify it and tell me about it.");
            AddQuickButton(quickBar, "Diagnose",      "Diagnose the current signal quality and suggest improvements.");
            AddQuickButton(quickBar, "Best Settings", "Apply the best SDR# settings for the current frequency and signal type.");

            // Chat history
            _chatHistory = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = BgDark,
                ForeColor = TextMain,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Font = new Font("Segoe UI", 10f),
                Padding = new Padding(8)
            };

            // Input area
            var inputPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                BackColor = BgMedium,
                Padding = new Padding(6),
                ColumnCount = 3,
                RowCount = 1
            };
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            inputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

            _chatInput = new TextBox
            {
                Multiline = true,
                BackColor = BgLight,
                ForeColor = TextMain,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.5f),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 8, 0),
                PlaceholderText = "Ask anything about your radio..."
            };
            _chatInput.KeyDown += ChatInput_KeyDown;

            _sendBtn = DarkButton("Send", AccentBlue);
            _sendBtn.Dock = DockStyle.Fill;
            _sendBtn.Margin = new Padding(0, 0, 8, 0);
            _sendBtn.Click += SendBtn_Click;

            _clearBtn = DarkButton("Clear", BgLight);
            _clearBtn.Dock = DockStyle.Fill;
            _clearBtn.Margin = new Padding(0);
            _clearBtn.ForeColor = TextDim;
            _clearBtn.Click += (_, _) => { _chatHistory.Clear(); _llm.ClearHistory(); };

            _statusLabel = new Label
            {
                Text = "Ready",
                ForeColor = TextDim,
                BackColor = Color.Transparent,
                AutoSize = true,
                Font = new Font("Segoe UI", 8f)
            };
            _statusLabel.Visible = false;

            inputPanel.Controls.Add(_chatInput, 0, 0);
            inputPanel.Controls.Add(_sendBtn, 1, 0);
            inputPanel.Controls.Add(_clearBtn, 2, 0);

            page.Controls.Add(inputPanel);
            page.Controls.Add(_chatHistory);
            page.Controls.Add(quickBar);
            page.Controls.Add(statusBar);

            AppendAiMessage("Hello! I'm your AI radio assistant. I can help you:\n" +
                "• Identify and tune to frequencies\n" +
                "• Diagnose signal quality\n" +
                "• Apply optimal settings automatically\n" +
                "• Answer questions about radio and SDR\n\n" +
                "Just ask — or use the quick buttons above.");

            return page;
        }

        // ── Diagnostics Tab ───────────────────────────────────────────────────────

        private TabPage BuildDiagnosticsTab()
        {
            var page = new TabPage("Diagnostics") { BackColor = BgDark, ForeColor = TextMain };

            _runDiagBtn = DarkButton("Run AI Diagnostic", AccentBlue);
            _runDiagBtn.Dock = DockStyle.Top;
            _runDiagBtn.Height = 38;
            _runDiagBtn.Margin = new Padding(0, 0, 0, 8);
            _runDiagBtn.Click += RunDiagBtn_Click;

            _quickActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = BgDark,
                Padding = new Padding(8, 10, 8, 10),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };
            _quickActions.Resize += (_, _) =>
            {
                int btnW = Math.Max(140, (_quickActions.ClientSize.Width - 24) / 2);
                int pad = 8, gap = 8;
                int cols = Math.Max(1, (_quickActions.ClientSize.Width - pad * 2 + gap) / (btnW + gap));
                int totalW = cols * btnW + (cols - 1) * gap;
                int leftPad = Math.Max(pad, (_quickActions.ClientSize.Width - totalW) / 2);
                _quickActions.Padding = new Padding(leftPad, pad, pad, pad);
                foreach (Control control in _quickActions.Controls)
                    control.Width = btnW;
            };

            string[] actions =
            {
                "Tune FM Radio",    "Tune AM Broadcast",
                "Aviation Comms",   "Marine VHF",
                "NOAA Weather",     "Amateur 2m FM",
                "ADS-B Aircraft",   "40m Amateur HF"
            };
            foreach (string action in actions)
            {
                var btn = DarkButton(action, BgLight);
                btn.Size = new Size(160, 30);
                btn.ForeColor = AccentBlue;
                btn.Margin = new Padding(0, 0, 8, 8);
                btn.Click += (_, _) => SendQuickPrompt($"Apply settings and tune to {action}.");
                _quickActions.Controls.Add(btn);
            }

            _diagReport = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = BgDark,
                ForeColor = TextMain,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Font = new Font("Consolas", 9f),
                Padding = new Padding(6)
            };

            page.Controls.Add(_diagReport);
            page.Controls.Add(_quickActions);
            page.Controls.Add(_runDiagBtn);

            return page;
        }

        // ── Monitor Tab ───────────────────────────────────────────────────────────

        private TabPage BuildMonitorTab()
        {
            var page = new TabPage("Monitor") { BackColor = BgDark, ForeColor = TextMain };

            var topPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = BgMedium,
                Padding = new Padding(10, 10, 10, 8),
                ColumnCount = 2,
                RowCount = 4
            };
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var sectionLabel = new Label
            {
                Text = "LIVE ACTIVITY MONITOR",
                ForeColor = AccentBlue,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold)
            };
            topPanel.SetColumnSpan(sectionLabel, 2);
            topPanel.Controls.Add(sectionLabel, 0, 0);

            var threshLabel = new Label
            {
                Text = "Min power (dBfs):",
                ForeColor = TextDim,
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 0, 8, 8)
            };
            _monThreshBox = new TextBox
            {
                Text = "-70",
                BackColor = BgLight,
                ForeColor = TextMain,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0, 0, 0, 8)
            };
            topPanel.Controls.Add(threshLabel, 0, 1);
            topPanel.Controls.Add(Bordered(_monThreshBox), 1, 1);

            _autoJumpCheck = new CheckBox
            {
                Text = "Auto-Jump on new transmission",
                ForeColor = TextMain,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8),
                Checked = false,
                FlatStyle = FlatStyle.Flat
            };
            topPanel.SetColumnSpan(_autoJumpCheck, 2);
            topPanel.Controls.Add(_autoJumpCheck, 0, 2);

            var monitorActionRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0)
            };
            monitorActionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            monitorActionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));

            _monStartBtn = DarkButton("Start Monitor", AccentGreen);
            _monStartBtn.Dock = DockStyle.Fill;
            _monStartBtn.Height = 30;
            _monStartBtn.Margin = new Padding(0, 0, 10, 0);
            _monStartBtn.Click += MonStartBtn_Click;

            _monStatusLabel = new Label
            {
                Text = "Stopped  —  reads FFT without moving the tuner",
                ForeColor = TextDim,
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 7.5f)
            };

            monitorActionRow.Controls.Add(_monStartBtn, 0, 0);
            monitorActionRow.Controls.Add(_monStatusLabel, 1, 0);
            topPanel.SetColumnSpan(monitorActionRow, 2);
            topPanel.Controls.Add(monitorActionRow, 0, 3);

            var sweepPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = BgDark,
                Padding = new Padding(10, 10, 10, 8),
                ColumnCount = 2,
                RowCount = 5
            };
            sweepPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            sweepPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var sweepLabel = new Label
            {
                Text = "FAST SWEEP",
                ForeColor = AccentBlue,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold)
            };
            var sweepNote = new Label
            {
                Text = "Moves center, not VFO, then returns when done",
                ForeColor = TextDim,
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 8),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 7.5f)
            };
            sweepPanel.Controls.Add(sweepLabel, 0, 0);
            sweepPanel.Controls.Add(sweepNote, 1, 0);

            var startLabel = new Label
            {
                Text = "Start Hz:",
                ForeColor = TextDim,
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 0, 8, 8)
            };
            _fastSweepStartBox = new TextBox
            {
                Text = "118000000",
                BackColor = BgLight,
                ForeColor = TextMain,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0, 0, 0, 8)
            };
            sweepPanel.Controls.Add(startLabel, 0, 1);
            sweepPanel.Controls.Add(Bordered(_fastSweepStartBox), 1, 1);

            var endLabel = new Label
            {
                Text = "End Hz:",
                ForeColor = TextDim,
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 0, 8, 8)
            };
            _fastSweepEndBox = new TextBox
            {
                Text = "137000000",
                BackColor = BgLight,
                ForeColor = TextMain,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0, 0, 0, 8)
            };
            sweepPanel.Controls.Add(endLabel, 0, 2);
            sweepPanel.Controls.Add(Bordered(_fastSweepEndBox), 1, 2);

            _fastSweepRepeatCheck = new CheckBox
            {
                Text = "Repeat",
                ForeColor = TextMain,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8),
                FlatStyle = FlatStyle.Flat
            };
            sweepPanel.SetColumnSpan(_fastSweepRepeatCheck, 2);
            sweepPanel.Controls.Add(_fastSweepRepeatCheck, 0, 3);

            var sweepActionRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0)
            };
            sweepActionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            sweepActionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));

            _fastSweepBtn = DarkButton("Fast Sweep", AccentBlue);
            _fastSweepBtn.Dock = DockStyle.Fill;
            _fastSweepBtn.Height = 30;
            _fastSweepBtn.Margin = new Padding(0, 0, 10, 0);
            _fastSweepBtn.Click += FastSweepBtn_Click;

            _fastSweepStatusLabel = new Label
            {
                Text = "",
                ForeColor = TextDim,
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 7.5f)
            };

            sweepActionRow.Controls.Add(_fastSweepBtn, 0, 0);
            sweepActionRow.Controls.Add(_fastSweepStatusLabel, 1, 0);
            sweepPanel.SetColumnSpan(sweepActionRow, 2);
            sweepPanel.Controls.Add(sweepActionRow, 0, 4);

            // Column header
            var listHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 22,
                BackColor = BgMedium,
                Padding = new Padding(6, 3, 6, 0)
            };
            listHeader.Controls.Add(new Label
            {
                Text = "FREQUENCY          POWER     LAST SEEN",
                ForeColor = TextDim, BackColor = Color.Transparent,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Consolas", 7.5f)
            });

            // Activity list
            _activityList = new ListView
            {
                Dock = DockStyle.Fill,
                BackColor = BgDark,
                ForeColor = TextMain,
                BorderStyle = BorderStyle.None,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = new Font("Consolas", 8.5f)
            };
            _activityList.Columns.Add("Frequency", 120);
            _activityList.Columns.Add("Power",     70);
            _activityList.Columns.Add("Status",    80);
            _activityList.Columns.Add("Last Seen", 70);
            _activityList.DoubleClick += ActivityList_DoubleClick;

            // Bottom bar
            var bottomBar = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                BackColor = BgMedium,
                ColumnCount = 2,
                Padding = new Padding(2),
                Margin = new Padding(0)
            };
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            var tuneBtn = DarkButton("Tune to Selected", AccentBlue);
            tuneBtn.Dock = DockStyle.Fill;
            tuneBtn.Margin = new Padding(0, 0, 4, 0);
            tuneBtn.Click += (_, _) => TuneToSelectedActivity();

            var askBtn = DarkButton("Ask AI", BgLight);
            askBtn.ForeColor = AccentBlue;
            askBtn.Dock = DockStyle.Fill;
            askBtn.Margin = new Padding(0);
            askBtn.Click += ActivityAskAi_Click;

            bottomBar.Controls.Add(tuneBtn, 0, 0);
            bottomBar.Controls.Add(askBtn, 1, 0);

            page.Controls.Add(_activityList);
            page.Controls.Add(listHeader);
            page.Controls.Add(bottomBar);
            page.Controls.Add(sweepPanel);
            page.Controls.Add(topPanel);

            return page;
        }

        // ── Scanner Tab ───────────────────────────────────────────────────────────

        private TabPage BuildScannerTab()
        {
            var page = new TabPage("Scanner") { BackColor = BgDark, ForeColor = TextMain };

            var controls = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                BackColor = BgDark,
                Padding = new Padding(8, 10, 8, 8)
            };
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int r = 0;
            void AddScanRow(int h = 30) => controls.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
            void AddScanLabel(string text, int row) =>
                controls.Controls.Add(new Label
                {
                    Text = text, ForeColor = TextDim, BackColor = Color.Transparent,
                    Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight,
                    Padding = new Padding(0, 0, 8, 0)
                }, 0, row);

            // Band preset
            AddScanRow();
            AddScanLabel("Band:", r);
            _bandCombo = new ComboBox
            {
                Dock = DockStyle.Fill, BackColor = BgLight, ForeColor = TextMain,
                FlatStyle = FlatStyle.Flat, DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 2, 0, 6)
            };
            foreach (var b in Services.ScannerService.Bands) _bandCombo.Items.Add(b.Name);
            _bandCombo.SelectedIndex = 0;
            _bandCombo.SelectedIndexChanged += BandCombo_Changed;
            controls.Controls.Add(_bandCombo, 1, r++);

            // Start / End / Step / Dwell / Threshold
            AddScanRow(); AddScanLabel("Start Hz:", r);
            _scanStartBox = ScanInput("87500000");
            controls.Controls.Add(Bordered(_scanStartBox), 1, r++);

            AddScanRow(); AddScanLabel("End Hz:", r);
            _scanEndBox = ScanInput("108000000");
            controls.Controls.Add(Bordered(_scanEndBox), 1, r++);

            AddScanRow(); AddScanLabel("Step Hz:", r);
            _scanStepBox = ScanInput("200000");
            controls.Controls.Add(Bordered(_scanStepBox), 1, r++);

            AddScanRow(); AddScanLabel("Dwell ms:", r);
            _scanDwellBox = ScanInput("400");
            controls.Controls.Add(Bordered(_scanDwellBox), 1, r++);

            AddScanRow(); AddScanLabel("Min SNR:", r);
            _scanThreshBox = ScanInput("15");
            controls.Controls.Add(Bordered(_scanThreshBox), 1, r++);

            // Mode
            AddScanRow();
            AddScanLabel("Mode:", r);
            _scanModeCombo = new ComboBox
            {
                Dock = DockStyle.Fill, BackColor = BgLight, ForeColor = TextMain,
                FlatStyle = FlatStyle.Flat, DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 2, 0, 6)
            };
            _scanModeCombo.Items.AddRange(new object[]
            {
                "Seek  — stop on signal, resume on silence",
                "Sweep — log hits and keep going",
                "Monitor  — repeat range continuously"
            });
            _scanModeCombo.SelectedIndex = 0;
            controls.Controls.Add(_scanModeCombo, 1, r++);

            // Start button
            AddScanRow(38);
            _scanStartBtn = DarkButton("Start Scan", AccentGreen);
            _scanStartBtn.Dock = DockStyle.Fill;
            _scanStartBtn.Margin = new Padding(0, 6, 0, 4);
            _scanStartBtn.Click += ScanStartBtn_Click;
            controls.SetColumnSpan(_scanStartBtn, 2);
            controls.Controls.Add(_scanStartBtn, 0, r++);

            // Progress
            AddScanRow(24);
            _scanFreqLabel = new Label
            {
                Text = "—", ForeColor = AccentBlue, BackColor = Color.Transparent,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5f)
            };
            controls.SetColumnSpan(_scanFreqLabel, 2);
            controls.Controls.Add(_scanFreqLabel, 0, r++);

            AddScanRow(12);
            _scanProgress = new ProgressBar
            {
                Dock = DockStyle.Fill, BackColor = BgLight, ForeColor = AccentBlue,
                Style = ProgressBarStyle.Continuous, Minimum = 0, Maximum = 100
            };
            controls.SetColumnSpan(_scanProgress, 2);
            controls.Controls.Add(_scanProgress, 0, r++);

            // Hit log header
            var hitHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 24, ColumnCount = 2,
                BackColor = BgMedium, Padding = new Padding(6, 3, 6, 3)
            };
            hitHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            hitHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            hitHeader.Controls.Add(new Label
            {
                Text = "Signals Found", ForeColor = TextDim, BackColor = Color.Transparent,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8f)
            }, 0, 0);

            var clearHitsBtn = DarkButton("Clear", BgLight);
            clearHitsBtn.ForeColor = TextDim;
            clearHitsBtn.Dock = DockStyle.Fill;
            clearHitsBtn.Font = new Font("Segoe UI", 7.5f);
            clearHitsBtn.Click += (_, _) => { _scanHits.Clear(); _scanHitList.Items.Clear(); };
            hitHeader.Controls.Add(clearHitsBtn, 1, 0);

            // Hit list
            _scanHitList = new ListBox
            {
                Dock = DockStyle.Fill, BackColor = BgDark, ForeColor = TextMain,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 8.5f),
                SelectionMode = SelectionMode.One
            };
            _scanHitList.DoubleClick += ScanHitList_DoubleClick;

            // Bottom bar
            var bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = BgMedium, Padding = new Padding(0, 4, 0, 4) };
            _scanAskAiBtn = DarkButton("Ask AI about selected signal", AccentBlue);
            _scanAskAiBtn.Dock = DockStyle.Fill;
            _scanAskAiBtn.Click += ScanAskAi_Click;
            bottomBar.Controls.Add(_scanAskAiBtn);

            _scanStatusLabel = new Label
            {
                Text = "Stopped", ForeColor = TextDim, BackColor = Color.Transparent,
                AutoSize = true, Font = new Font("Segoe UI", 8f),
                Padding = new Padding(6, 0, 0, 0)
            };

            page.Controls.Add(_scanHitList);
            page.Controls.Add(hitHeader);
            page.Controls.Add(bottomBar);
            page.Controls.Add(controls);

            return page;
        }

        // ── Settings Tab ──────────────────────────────────────────────────────────

        private TabPage BuildSettingsTab()
        {
            var page = new TabPage("Settings") { BackColor = BgDark, ForeColor = TextMain };
            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgDark,
                AutoScroll = true,
                Padding = new Padding(12, 12, 12, 12)
            };

            const int INPUT_H  = 30;
            const int COMBO_H  = 30;
            const int PRESET_H = 86;
            const int CHECK_H  = 30;
            const int BTN_H    = 36;
            const int STATUS_H = 30;
            const int GAP_H    = 14;

            // Rows: provider, url, presets, gap, apikey, model, timeout, gap, mode, gap, save, test, status
            int[] rowHeights = { COMBO_H, INPUT_H, PRESET_H, GAP_H, INPUT_H, INPUT_H, INPUT_H, GAP_H, CHECK_H, GAP_H, BTN_H, BTN_H, STATUS_H };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                ColumnCount = 2,
                RowCount = rowHeights.Length,
                BackColor = BgDark,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int h in rowHeights)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
            layout.Height = rowHeights.Sum();

            int r = 0;

            // Provider
            layout.Controls.Add(SettingsLabel("Provider:"), 0, r);
            _providerCombo = new ComboBox
            {
                Dock = DockStyle.Fill, BackColor = BgLight, ForeColor = TextMain,
                FlatStyle = FlatStyle.Flat, DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 3, 0, 5)
            };
            _providerCombo.Items.AddRange(new object[] { "Anthropic (Claude)", "OpenAI Compatible" });
            _providerCombo.SelectedIndex = 0;
            _providerCombo.SelectedIndexChanged += (_, _) => UpdateProviderVisibility();
            layout.Controls.Add(_providerCombo, 1, r++);

            // Base URL
            var urlLabel = SettingsLabel("Base URL:");
            layout.Controls.Add(urlLabel, 0, r);
            _baseUrlBox = new TextBox
            {
                Dock = DockStyle.Fill, BackColor = BgLight, ForeColor = TextMain,
                BorderStyle = BorderStyle.None,
                PlaceholderText = "http://localhost:11434/v1",
                Margin = new Padding(0, 4, 0, 4)
            };
            var urlBordered = Bordered(_baseUrlBox);
            layout.Controls.Add(urlBordered, 1, r++);
            _openAiOnlyControls.Add(urlLabel);
            _openAiOnlyControls.Add(urlBordered);

            // Presets
            var presetSpacer = new Label { BackColor = Color.Transparent };
            layout.Controls.Add(presetSpacer, 0, r);
            var presets = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent, WrapContents = true,
                Padding = new Padding(0, 4, 0, 0)
            };
            AddPresetBtn(presets, "Ollama",     "http://localhost:11434/v1");
            AddPresetBtn(presets, "LM Studio",  "http://localhost:1234/v1");
            AddPresetBtn(presets, "llama.cpp",  "http://localhost:8080/v1");
            AddPresetBtn(presets, "Groq",       "https://api.groq.com/openai/v1");
            AddPresetBtn(presets, "OpenAI",     "https://api.openai.com/v1");
            AddPresetBtn(presets, "OpenRouter", "https://openrouter.ai/api/v1");
            layout.Controls.Add(presets, 1, r++);
            _openAiOnlyControls.Add(presetSpacer);
            _openAiOnlyControls.Add(presets);

            r++; // gap row

            // API Key
            layout.Controls.Add(SettingsLabel("API Key:"), 0, r);
            _apiKeyBox = new TextBox
            {
                Dock = DockStyle.Fill, BackColor = BgLight, ForeColor = TextMain,
                BorderStyle = BorderStyle.None, PasswordChar = '●',
                PlaceholderText = "sk-...  (blank for local models)",
                Margin = new Padding(0, 4, 0, 4)
            };
            layout.Controls.Add(Bordered(_apiKeyBox), 1, r++);

            // Model
            layout.Controls.Add(SettingsLabel("Model:"), 0, r);
            _modelBox = new TextBox
            {
                Dock = DockStyle.Fill, BackColor = BgLight, ForeColor = TextMain,
                BorderStyle = BorderStyle.None,
                PlaceholderText = "claude-opus-4-6 / llama3 / gpt-4o",
                Margin = new Padding(0, 4, 0, 4)
            };
            layout.Controls.Add(Bordered(_modelBox), 1, r++);

            // Timeout
            layout.Controls.Add(SettingsLabel("Timeout (s):"), 0, r);
            _timeoutBox = new TextBox
            {
                Dock = DockStyle.Fill, BackColor = BgLight, ForeColor = TextMain,
                BorderStyle = BorderStyle.None,
                PlaceholderText = "120", Text = "120",
                Margin = new Padding(0, 4, 0, 4)
            };
            layout.Controls.Add(Bordered(_timeoutBox), 1, r++);

            r++; // gap row

            // Mode
            layout.Controls.Add(SettingsLabel("Mode:"), 0, r);
            _beginnerCheck = new CheckBox
            {
                AutoSize = false, Dock = DockStyle.Fill,
                Checked = true, BackColor = Color.Transparent, ForeColor = TextMain,
                Text = "Beginner  (plain language)",
                Margin = new Padding(0, 4, 0, 0),
                FlatStyle = FlatStyle.Flat
            };
            layout.Controls.Add(_beginnerCheck, 1, r++);

            r++; // gap row

            // Save
            _saveSettingsBtn = DarkButton("Save Settings", AccentBlue);
            _saveSettingsBtn.Dock = DockStyle.Fill;
            _saveSettingsBtn.Margin = new Padding(0, 0, 0, 6);
            _saveSettingsBtn.Click += SaveSettings_Click;
            layout.SetColumnSpan(_saveSettingsBtn, 2);
            layout.Controls.Add(_saveSettingsBtn, 0, r++);

            // Test Connection
            _testBtn = DarkButton("Test Connection", BgLight);
            _testBtn.ForeColor = AccentBlue;
            _testBtn.Dock = DockStyle.Fill;
            _testBtn.Margin = new Padding(0, 0, 0, 6);
            _testBtn.Click += TestBtn_Click;
            layout.SetColumnSpan(_testBtn, 2);
            layout.Controls.Add(_testBtn, 0, r++);

            // Status
            _settingsStatus = new Label
            {
                Dock = DockStyle.Fill, BackColor = Color.Transparent,
                ForeColor = AccentGreen, TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8.5f)
            };
            layout.SetColumnSpan(_settingsStatus, 2);
            layout.Controls.Add(_settingsStatus, 0, r++);

            scroll.Controls.Add(layout);
            page.Controls.Add(scroll);

            UpdateProviderVisibility();
            return page;
        }

        // ── Control field declarations ────────────────────────────────────────────

        private TabControl _tabs = null!;

        // Chat tab
        private RichTextBox _chatHistory = null!;
        private TextBox     _chatInput   = null!;
        private Button      _sendBtn     = null!;
        private Button      _clearBtn    = null!;
        private Label       _statusLabel = null!;

        // Signal status bar
        private Label _freqLabel    = null!;
        private Label _modeLabel    = null!;
        private Label _snrLabel     = null!;
        private Label _carrierLabel = null!;

        // Diagnostics tab
        private RichTextBox     _diagReport   = null!;
        private Button          _runDiagBtn   = null!;
        private FlowLayoutPanel _quickActions = null!;

        // Settings tab
        private ComboBox  _providerCombo    = null!;
        private TextBox   _baseUrlBox       = null!;
        private TextBox   _apiKeyBox        = null!;
        private TextBox   _modelBox         = null!;
        private TextBox   _timeoutBox       = null!;
        private CheckBox  _beginnerCheck    = null!;
        private Button    _saveSettingsBtn  = null!;
        private Button    _testBtn          = null!;
        private Label     _settingsStatus   = null!;

        // Monitor tab
        private CheckBox  _autoJumpCheck        = null!;
        private TextBox   _monThreshBox         = null!;
        private Button    _monStartBtn          = null!;
        private ListView  _activityList         = null!;
        private Label     _monStatusLabel       = null!;
        private Button    _fastSweepBtn         = null!;
        private TextBox   _fastSweepStartBox    = null!;
        private TextBox   _fastSweepEndBox      = null!;
        private CheckBox  _fastSweepRepeatCheck = null!;
        private Label     _fastSweepStatusLabel = null!;

        // Scanner tab
        private ComboBox    _bandCombo      = null!;
        private TextBox     _scanStartBox   = null!;
        private TextBox     _scanEndBox     = null!;
        private TextBox     _scanStepBox    = null!;
        private TextBox     _scanDwellBox   = null!;
        private TextBox     _scanThreshBox  = null!;
        private ComboBox    _scanModeCombo  = null!;
        private Button      _scanStartBtn   = null!;
        private Label       _scanFreqLabel  = null!;
        private ProgressBar _scanProgress   = null!;
        private ListBox     _scanHitList    = null!;
        private Button      _scanAskAiBtn   = null!;
        private Label       _scanStatusLabel = null!;
    }
}
