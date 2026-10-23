// SE2 Workshop UGC Fix: narrow owner-only repair of the known oversized
// internal BaseMetadata.Description in Steam UGC developer metadata.
// Read-only analysis always precedes any explicit update. Public Workshop
// description and content are NEVER edited by this app.
using Steamworks;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SE2WorkshopUGCFix;

internal static class Program
{
    internal static bool FixtureLabEnabled { get; private set; }

    [STAThread]
    private static int Main(string[] args)
    {
        // Deliberate developer opt-in: production/default UI cannot create UGC items.
#if SE2_INTERNAL_LAB
        FixtureLabEnabled = args.Length == 1 && args[0] == "--fixture-lab";
#else
        // The candidate user build cannot enter the destructive fixture lab,
        // even if someone manually launches the exe with --fixture-lab.
        FixtureLabEnabled = false;
#endif
        // The offline UI smoke mode never initializes Steam and cannot submit updates.
        // Exit codes make this executable usable by a Windows test runner.
        if (args.Length == 1 && args[0] == "--smoke-ui")
        {
#if SE2_INTERNAL_LAB
            FixtureLabEnabled = true; // Validate the lab UI without calling Steam.
#endif
            try
            {
                ApplicationConfiguration.Initialize();
                using var main = new FixForm();
                using var recovery = new RecoveryPreviewForm(12345678UL);
#if SE2_INTERNAL_LAB
                using var fixtureDialog = new TestItemCreateForm();
                using var suiteDialog = new DisposableE2EForm(12345678UL);
#endif
                main.ShowInTaskbar = false;
                main.Opacity = 0;
                main.Show();
                Application.DoEvents();
                var controls = Descendants(main).OfType<Button>().ToArray();
                var repair = controls.Single(c => c.Text.Contains("REPAIR", StringComparison.OrdinalIgnoreCase));
                var preview = controls.Single(c => c.Text == "RECOVERY PREVIEW");
                var scan = controls.Single(c => c.Text.Contains("CHECK METADATA", StringComparison.OrdinalIgnoreCase));
                var probe = controls.Single(c => c.Text == "READ-ONLY UGC PROBE");
                if (repair.Enabled || preview.Enabled || !scan.Enabled || !probe.Enabled)
                    throw new InvalidOperationException("Unsafe initial GUI button state.");
#if SE2_INTERNAL_LAB
                var fixture = controls.Single(c => c.Text == "CREATE EMPTY ITEM");
                var e2e = controls.Single(c => c.Text == "RUN FULL MOD E2E");
                if (!fixture.Enabled || !fixture.Visible || !e2e.Enabled || !e2e.Visible)
                    throw new InvalidOperationException("Developer fixture lab controls missing.");
#else
                if (controls.Any(c => c.Text == "CREATE EMPTY ITEM" ||
                    c.Text == "RUN FULL MOD E2E"))
                    throw new InvalidOperationException("Consumer build exposed developer-only tools.");
#endif
                // Verify the two footer groups and that labels fit their buttons.
                var actionGroups = Descendants(main).OfType<GroupBox>().ToArray();
                if (actionGroups.Length != 2 ||
                    !actionGroups.Any(g => g.Text == "BACKUPS / REPORTS") ||
                    !actionGroups.Any(g => g.Text == "TOOLS / RECOVERY"))
                    throw new InvalidOperationException("Footer action groups were not initialized.");
                foreach (var group in actionGroups) {
                    var buttons = Descendants(group).OfType<Button>().ToArray();
                    if (buttons.Length != 2)
                        throw new InvalidOperationException("Footer group must contain exactly two actions.");
                    foreach (var button in buttons) {
                        var measured = TextRenderer.MeasureText(button.Text, button.Font);
                        if (button.ClientSize.Width < Math.Max(170, measured.Width + 18) ||
                            button.ClientSize.Height < 34)
                            throw new InvalidOperationException("A footer button is too small for its caption.");
                    }
                }
                // Regression: Windows native DPI previously rendered the UGC
                // probe caption beneath the next information row.
                var statusLabel = Descendants(main).OfType<Label>()
                    .Single(l => l.Text.StartsWith("Ready.", StringComparison.Ordinal));
                var statusBounds = statusLabel.RectangleToScreen(statusLabel.ClientRectangle);
                var probeBounds = probe.RectangleToScreen(probe.ClientRectangle);
                var statsLabel = Descendants(main).OfType<Label>()
                    .Single(l => l.Text == "No item inspected yet.");
                var statsBounds = statsLabel.RectangleToScreen(statsLabel.ClientRectangle);
                if (probeBounds.Height < 30 || statusBounds.Bottom > probeBounds.Top ||
                    probeBounds.Bottom > statsBounds.Top)
                    throw new InvalidOperationException(
                        "Diagnostic probe overlaps the metadata summary at native Windows DPI.");
#if SE2_INTERNAL_LAB
                if (Descendants(fixtureDialog).OfType<Button>()
                    .Single(c => c.Text == "CREATE ONE EMPTY TEST ITEM").Enabled)
                    throw new InvalidOperationException("Test-item creation must start locked.");
                if (Descendants(suiteDialog).OfType<Button>()
                    .Single(c => c.Text.StartsWith("PREPARE ONE GUI RESTORE TEST", StringComparison.Ordinal))
                    .Enabled)
                    throw new InvalidOperationException(
                        "Single-fixture preparation must start locked without exact approval.");
#endif
                if (!recovery.Text.Contains("Recovery Preview", StringComparison.Ordinal))
                    throw new InvalidOperationException("Recovery preview form failed to initialize.");
                recovery.ShowInTaskbar = false;
                recovery.Opacity = 0;
                recovery.Show();
                Application.DoEvents();
                if (!Descendants(recovery).OfType<Button>()
                    .Any(c => c.Text == "BUILD READ-ONLY RECOVERY PREVIEW"))
                    throw new InvalidOperationException("Recovery preview button missing.");
                if (Descendants(recovery).OfType<Button>()
                    .Single(c => c.Text.StartsWith("RESTORE MY WORKSHOP METADATA", StringComparison.Ordinal)).Enabled)
                    throw new InvalidOperationException("Recovery APPLY must start locked until verified preview and typed consent.");
                recovery.Hide();
                main.Hide();
                return 0;
            }
            catch (Exception)
            {
                // WinExe has no console; the PowerShell/batch caller checks exit code 1.
                return 1;
            }
        }

        // AppID is set before SteamAPI.Init; no steam_appid.txt or account secrets.
        Environment.SetEnvironmentVariable("SteamAppId", "1133870");
        Environment.SetEnvironmentVariable("SteamGameId", "1133870");
        ApplicationConfiguration.Initialize();
        Application.Run(new FixForm());
        return 0;
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control grandchild in Descendants(child))
                yield return grandchild;
        }
    }
}

internal sealed class FixForm : Form
{
    private readonly TextBox _workshop = new() { PlaceholderText = "Paste your Steam Workshop URL or item ID", Dock = DockStyle.Fill };
    private readonly Button _scan = new() { Text = "1   CHECK METADATA", Height = 44, Dock = DockStyle.Fill };
    private readonly Button _fixture = new() { Text = "CREATE EMPTY ITEM", Dock = DockStyle.Fill, Enabled = false };
    private readonly Button _e2e = new() { Text = "RUN FULL MOD E2E", Dock = DockStyle.Fill, Enabled = false };
    private readonly Button _probe = new() { Text = "READ-ONLY UGC PROBE", Dock = DockStyle.Fill,
        Font = new System.Drawing.Font("Segoe UI", 9F) };
    private readonly Button _repair = new() { Text = "2   REPAIR MY ITEM", Height = 44, Dock = DockStyle.Fill, Enabled = false };
    private readonly Button _recover = new() { Text = "RECOVERY PREVIEW", Height = 36, Dock = DockStyle.Fill, Enabled = false };
    private readonly Button _preflight = new() { Text = "DESCRIPTION PREFLIGHT", Height = 36, Dock = DockStyle.Fill };
    private readonly Button _report = new() { Text = "COPY SAFE REPORT", Height = 36, Dock = DockStyle.Fill, Enabled = false };
    private readonly Button _backup = new() { Text = "OPEN BACKUP FOLDER", Height = 36, Dock = DockStyle.Fill, Enabled = false };
    private readonly Label _status = new() { AutoSize = false, Text = "Ready. Steam must be running; close SE2 and ModHUB." };
    private readonly Label _stats = new() { AutoSize = false, Text = "No item inspected yet.", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = ModernUi.SurfaceRaised, ForeColor = ModernUi.Text, BorderStyle = BorderStyle.None };
    private ScanInfo? _current;
    private string? _lastBackupPath;
    private bool _busy;

    public FixForm()
    {
        Text = "SE2 Workshop UGC Metadata Fix   •   v0.5.0-preview.1 (OWNER-ONLY RECOVERY)";
        // Reserve actual vertical space for the grouped actions and security note.
        // The previous 33px toolbar row clipped buttons at normal Windows DPI.
        ClientSize = new System.Drawing.Size(960, Program.FixtureLabEnabled ? 900 : 830);
        MinimumSize = new System.Drawing.Size(850, Program.FixtureLabEnabled ? 820 : 750);
        StartPosition = FormStartPosition.CenterScreen;
        ModernUi.Window(this);

        var layout = new TableLayoutPanel {
            Dock = DockStyle.Fill, Padding = new Padding(24),
            BackColor = ModernUi.Background, ColumnCount = 1, RowCount = 10
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(layout);

        var title = ModernUi.Label("SE2   /   WORKSHOP RECOVERY", 20F, true);
        title.ForeColor = ModernUi.Text;
        layout.Controls.Add(title, 0, 0);
        var intro = ModernUi.Label(
            "STEAM APP 1133870    •    Diagnose, backups and owner-approved metadata recovery. " +
            "Unverified sources stay read-only.", 9.6F, false, ModernUi.Muted);
        layout.Controls.Add(intro, 0, 1);
        layout.Controls.Add(ModernUi.InputShell(_workshop, 13), 0, 2);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0) };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _scan.Margin = new Padding(2, 6, 9, 6);
        _repair.Margin = new Padding(9, 6, 2, 6);
        ModernUi.Button(_scan, primary: true);
        ModernUi.Button(_repair);
        actions.Controls.Add(_scan, 0, 0);
        actions.Controls.Add(_repair, 1, 0);
        layout.Controls.Add(actions, 0, 3);
        // Keep diagnostics in their own layout row. The previous nested 73/27
        // status/probe table could paint the probe caption beneath the next
        // auto-sized stats label on native Windows DPI (user Windows smoke).
        // The status is now a full-width line and tools get a separate toolbar.
        var statusArea = new TableLayoutPanel {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            BackColor = ModernUi.Surface, Padding = new Padding(12, 4, 12, 3),
            Margin = new Padding(2, 4, 2, 3)
        };
        statusArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        statusArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _status.Dock = DockStyle.Fill;
        _status.ForeColor = ModernUi.Muted;
        _status.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
        _status.Margin = new Padding(6, 0, 0, 0);
        _status.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        statusArea.Controls.Add(_status, 0, 0);

        bool fixtureLab = Program.FixtureLabEnabled;
        var diagnosticButtons = new TableLayoutPanel {
            Dock = DockStyle.Fill, ColumnCount = fixtureLab ? 3 : 2,
            RowCount = 1, Padding = new Padding(0), Margin = new Padding(0)
        };
        diagnosticButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        if (fixtureLab) {
            for (int i = 0; i < 3; i++)
                diagnosticButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3F));
        } else {
            diagnosticButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 69));
            diagnosticButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        }
        _fixture.Visible = fixtureLab;
        _fixture.Enabled = fixtureLab;
        _e2e.Visible = fixtureLab;
        _e2e.Enabled = fixtureLab;
        var diagnosticActions = fixtureLab
            ? new[] { _e2e, _fixture, _probe }
            : new[] { _probe };
        foreach (var action in diagnosticActions) {
            action.Margin = new Padding(4, 5, 4, 4);
            ModernUi.Button(action);
            action.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        }
        if (fixtureLab) {
            diagnosticButtons.Controls.Add(_e2e, 0, 0);
            diagnosticButtons.Controls.Add(_fixture, 1, 0);
            diagnosticButtons.Controls.Add(_probe, 2, 0);
        } else {
            diagnosticButtons.Controls.Add(_probe, 1, 0);
        }
        statusArea.Controls.Add(diagnosticButtons, 0, 1);
        layout.Controls.Add(statusArea, 0, 4);
        _stats.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold);
        _stats.ForeColor = ModernUi.Text;
        _stats.Padding = new Padding(6, 0, 0, 0);
        var summaryCard = ModernUi.Card(13);
        summaryCard.Controls.Add(_stats);
        layout.Controls.Add(summaryCard, 0, 5);
        var caption = ModernUi.Label("ACTIVITY  /  DIAGNOSTICS", 10F, true, ModernUi.Muted);
        layout.Controls.Add(caption, 0, 6);
        ModernUi.Input(_log, multiline: true);
        _log.BackColor = ModernUi.Surface;
        _log.ReadOnly = true;
        var logCard = ModernUi.Card(13);
        logCard.Controls.Add(_log);
        layout.Controls.Add(logCard, 0, 7);
        // Two accessible, clearly labelled groups rather than four compressed buttons.
        var outputActions = new TableLayoutPanel {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Margin = new Padding(0, 7, 0, 4)
        };
        outputActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        outputActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        outputActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outputActions.Controls.Add(CreateActionGroup("BACKUPS / REPORTS", _backup, _report), 0, 0);
        outputActions.Controls.Add(CreateActionGroup("TOOLS / RECOVERY", _preflight, _recover), 1, 0);
        layout.Controls.Add(outputActions, 0, 8);

        layout.Controls.Add(ModernUi.Label(
            "SECURITY   •   Steam writes require ownership and your explicit approval. Private backups stay on this PC.",
            9F, false, ModernUi.Muted), 0, 9);
        _scan.Click += async (_, _) => await ScanAsync();
        _probe.Click += async (_, _) => await ProbeAsync();
#if SE2_INTERNAL_LAB
        _e2e.Click += (_, _) => {
            if (_busy || !Program.FixtureLabEnabled) return;
            if (!SteamFix.TryGetId(_workshop.Text, out ulong selectedId)) {
                MessageBox.Show(this, "Enter the Workshop ID of your REAL published ModHUB test mod.",
                    "Missing E2E test ID", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using var dialog = new DisposableE2EForm(selectedId);
            dialog.ShowDialog(this);
        };
        _fixture.Click += (_, _) => {
            if (_busy) return;
            using var wizard = new TestItemCreateForm();
            wizard.ShowDialog(this);
            if (wizard.CreatedItemId is ulong id) {
                _workshop.Text = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _status.Text = "New test-item ID entered. Use READ-ONLY UGC PROBE; do NOT REPAIR.";
                _status.ForeColor = ModernUi.Warning;
                Append("Disposable Workshop test item created: " + id +
                    ". No content or metadata uploaded. Visibility not yet verified.");
            }
        };
#endif
        _repair.Click += async (_, _) => await RepairAsync();
        _backup.Click += (_, _) => OpenBackup();
        _report.Click += (_, _) => CopySafeReport();
        _recover.Click += (_, _) => {
            if (_busy || _current is null) return;
            using var preview = new RecoveryPreviewForm(_current.ItemId);
            preview.ShowDialog(this);
        };
        _preflight.Click += (_, _) => {
            if (_busy) return;
            using var dialog = new PreflightForm(_current?.OriginalRaw, _current?.ItemId);
            dialog.ShowDialog(this);
        };
        _workshop.TextChanged += (_, _) => {
            if (_busy) return;
            _current = null;
            _lastBackupPath = null;
            _backup.Enabled = false;
            _report.Enabled = false;
            _recover.Enabled = false;
            _stats.Text = "No item inspected yet.";
            _status.Text = "Workshop input changed. Run CHECK METADATA before any repair.";
            _status.ForeColor = ModernUi.Muted;
            UpdateRepairAvailability();
        };
        UpdateRepairAvailability();
        AcceptButton = _scan;
        FormClosing += (_, e) => {
            if (!_busy) return;
            e.Cancel = true;
            MessageBox.Show(this, "A Steam request is still running. Wait for the result before closing the app.",
                "Please wait", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        Append("This version diagnoses Workshop developer metadata. Owner-only recovery can restore verified backups or project-based proposals after explicit approval.");
        Append("Unverified sources remain preview-only. Healthy or foreign Workshop items are never rewritten.");
    }

    private static GroupBox CreateActionGroup(string title, Button first, Button second)
    {
        var group = new ModernGroupBox {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 26, 12, 10),
            Margin = new Padding(3, 0, 7, 0)
        };
        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        foreach (var button in new[] { first, second }) {
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(2, 4, 2, 4);
            ModernUi.Button(button);
            buttons.Controls.Add(button);
        }
        group.Controls.Add(buttons);
        return group;
    }

    private async Task ProbeAsync()
    {
        if (_busy) return;
        if (!SteamFix.TryGetId(_workshop.Text, out ulong id)) {
            MessageBox.Show(this, "Enter a valid Workshop item ID or Steam Workshop URL.",
                "Invalid Workshop ID", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        // A diagnostic must NEVER unlock the legacy Steam repair or recovery preview.
        _current = null;
        _lastBackupPath = null;
        _backup.Enabled = false;
        _report.Enabled = false;
        _recover.Enabled = false;
        _stats.Text = "Reading raw Steam API status...";
        SetBusy(true, "READ-ONLY: recording uncached Steam API results...");
        Append($"Read-only Steam UGC probe for item {id} started...");
        try {
            var info = await Task.Run(() => SteamReadProbe.Run(id));
            _lastBackupPath = Path.GetDirectoryName(info.ReportFile);
            _backup.Enabled = _lastBackupPath is not null && Directory.Exists(_lastBackupPath);
            _report.Enabled = File.Exists(info.ReportFile);
            _stats.Text = $"Probe: {info.Assessment.Status}  |  Bytes: {info.Assessment.MetadataBytes?.ToString("N0") ?? "UNKNOWN"}";
            _status.Text = info.Assessment.Status switch {
                SteamProbeStatus.Present => "UGC API RESULT: metadata present. Use CHECK METADATA.",
                SteamProbeStatus.ConfirmedEmpty => "UGC API RESULT: TRUE with 0 bytes — no Steam changes.",
                SteamProbeStatus.MetadataCallFailed => "UGC API RESULT: FALSE — missing metadata NOT confirmed.",
                _ => "READ-ONLY PROBE: see diagnostic result; no repair enabled."
            };
            _status.ForeColor = info.Assessment.Status == SteamProbeStatus.Present
                ? ModernUi.Success : ModernUi.Warning;
            Append(info.Assessment.Explanation);
            Append("Safe report available via COPY SAFE REPORT. No Steam update was sent.");
        } catch (Exception ex) {
            _status.Text = "READ-ONLY PROBE FAILED — Steam data unchanged.";
            _status.ForeColor = ModernUi.Danger;
            Append("Probe error: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Read-only Steam diagnostic failed",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        } finally {
            _current = null;
            SetBusy(false);
        }
    }

    private async Task ScanAsync()
    {
        if (_busy) return;
        if (!SteamFix.TryGetId(_workshop.Text, out ulong id)) {
            MessageBox.Show(this, "Enter a valid numeric Workshop ID or a URL containing ?id=...", "Invalid Workshop item", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _current = null;
        _lastBackupPath = null;
        _backup.Enabled = false;
        _report.Enabled = false;
        _recover.Enabled = false;
        _stats.Text = "Waiting for Steam metadata...";
        UpdateRepairAvailability();
        SetBusy(true, "Reading fresh data from Steam (no changes)...");
        Append($"Checking item {id}...");
        try {
            var info = await Task.Run(() => SteamFix.Scan(id));
            _current = info;
            _lastBackupPath = info.BackupDirectory;
            _backup.Enabled = Directory.Exists(info.BackupDirectory);
            _report.Enabled = File.Exists(Path.Combine(info.BackupDirectory, "report-SAFE-TO-SHARE.json"));
            var details = info.Diagnosis.Condition == MetadataCondition.Missing
                ? "Developer metadata: MISSING (0 bytes)"
                : $"Developer metadata: {info.OriginalBytes:N0} / {UniversalRecovery.SteamLimitBytes:N0} bytes";
            if (info.CandidateBytes is not null)
                details += $"  →  {info.CandidateBytes.Value:N0} bytes after patch";
            details += $"  |  Internal description: {info.DescriptionChars?.ToString("N0") ?? "N/A"} chars";
            _stats.Text = $"Item {info.ItemId}: {info.Title}\n{details}";
            Append($"Original SHA256: {info.OriginalHash}");
            Append($"Backed up original data: {info.BackupDirectory}");
            Append(info.Reason);
            Append("Detected: " + info.Diagnosis.Condition + " — " + info.Diagnosis.Explanation);
            var mayPreviewRecovery = info.OwnerMatches &&
                (info.Diagnosis.Condition is MetadataCondition.Missing or MetadataCondition.Malformed);
            _recover.Enabled = mayPreviewRecovery;
            if (!info.OwnerMatches) {
                _status.Text = "READ ONLY — This Workshop item is not owned by the signed-in Steam account.";
                _status.ForeColor = ModernUi.Warning;
            } else {
                switch (info.Diagnosis.Condition) {
                    case MetadataCondition.Healthy:
                        _status.Text = "HEALTHY — Metadata is valid. No repair necessary.";
                        _status.ForeColor = ModernUi.Success;
                        break;
                    case MetadataCondition.OversizedDescription when info.Eligible:
                        _status.Text = "REPAIR AVAILABLE — Oversized internal description detected.";
                        _status.ForeColor = ModernUi.Warning;
                        break;
                    case MetadataCondition.Missing:
                    case MetadataCondition.Malformed:
                        _status.Text = "RECOVERY PREVIEW AVAILABLE — Source verification and approval required before changes.";
                        _status.ForeColor = ModernUi.Warning;
                        break;
                    case MetadataCondition.IdentityConflict:
                        _status.Text = "UNSAFE METADATA IDENTITY — All repairs blocked.";
                        _status.ForeColor = ModernUi.Danger;
                        break;
                    default:
                        _status.Text = "NO SAFE AUTOMATIC REPAIR — See diagnosis.";
                        _status.ForeColor = ModernUi.Warning;
                        break;
                }
            }
        } catch (Exception ex) {
            _status.Text = "CHECK FAILED – nothing was changed on Steam.";
            _status.ForeColor = ModernUi.Danger;
            Append("CHECK FAILED: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Steam metadata check failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        } finally { SetBusy(false); }
    }

    private async Task RepairAsync()
    {
        if (_busy || _current is not { Eligible: true } info) return;
        if (!SteamFix.TryGetId(_workshop.Text, out var id) || id != info.ItemId) return;
        var message = $"This action WILL UPDATE developer metadata for YOUR Workshop item {id}.\n\n" +
            $"Current metadata: {info.OriginalBytes} bytes\nRepaired: {info.CandidateBytes} bytes\n\n" +
            "The ONLY intended change is to clear the INTERNAL FullMetadata.BaseMetadata.Description field.\n" +
            "The public Steam description, mod content, title, tags, visibility and image are NOT updated.\n\n" +
            "The original developer metadata has already been backed up locally.\n\n" +
            "Do you authorize this one-time Steam update?";
        if (MessageBox.Show(this, message, "Confirm update to your existing Workshop item", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        SetBusy(true, "Submitting a guarded metadata-only update...");
        Append($"Explicit consent received. Rechecking item {id} before submission...");
        try {
            var result = await Task.Run(() => SteamFix.Apply(info));
            Append(result);
            _status.Text = "STEAM UPDATE CONFIRMED. Now restart ModHUB and test the mod in SE2.";
            _status.ForeColor = ModernUi.Success;
            MessageBox.Show(this,
                "Steam accepted the metadata repair and a fresh read-back matched the candidate.\n\nRestart ModHUB and Space Engineers 2, then test in a disposable world. Do not overwrite existing saves until tested.",
                "Repair confirmed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        } catch (Exception ex) {
            _status.Text = "UPDATE NOT CONFIRMED – inspect activity / backup before retrying.";
            _status.ForeColor = ModernUi.Danger;
            Append("STOP: " + ex.Message);
            try { File.WriteAllText(Path.Combine(info.BackupDirectory, "last-error.txt"), ex.ToString()); }
            catch { /* Preserve original error; do not attempt update again. */ }
            MessageBox.Show(this, "STOP: " + ex.Message + "\n\nDo not retry without running a new read-only check.", "Repair stopped", MessageBoxButtons.OK, MessageBoxIcon.Error);
        } finally {
            _current = null; // Every update attempt requires a fresh scan before retry.
            SetBusy(false);
        }
    }

    private void SetBusy(bool active, string? status = null)
    {
        _busy = active;
        _scan.Enabled = !active;
        _probe.Enabled = !active;
        _fixture.Enabled = !active && Program.FixtureLabEnabled;
        _e2e.Enabled = !active && Program.FixtureLabEnabled;
        _preflight.Enabled = !active;
        _recover.Enabled = !active && _current is { OwnerMatches: true } &&
            (_current.Diagnosis.Condition is MetadataCondition.Missing or MetadataCondition.Malformed);
        _workshop.Enabled = !active;
        UpdateRepairAvailability();
        if (status is not null) { _status.Text = status; _status.ForeColor = ModernUi.Muted; }
        Cursor = active ? Cursors.WaitCursor : Cursors.Default;
    }

    private void UpdateRepairAvailability()
    {
        bool ready = !_busy && _current is { Eligible: true };
        _repair.Enabled = ready;
        _repair.Text = ready ? "2   REPAIR MY ITEM" : "2   REPAIR (CHECK FIRST)";
        _repair.BackColor = ready ? ModernUi.Success : ModernUi.SurfaceRaised;
        _repair.ForeColor = ready ? ModernUi.Background : ModernUi.Muted;
    }

    private void CopySafeReport()
    {
        if (_lastBackupPath is null) return;
        var file = Path.Combine(_lastBackupPath, "report-SAFE-TO-SHARE.json");
        if (!File.Exists(file)) return;
        try {
            Clipboard.SetText(File.ReadAllText(file));
            Append(_current is null
                ? "Read-only probe report copied. No original/candidate metadata JSON was created by this probe."
                : "Safe scan report copied. PRIVATE metadata backups remain on your computer.");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not copy report", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void Append(string line) => _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
    private void OpenBackup()
    {
        if (_lastBackupPath is null || !Directory.Exists(_lastBackupPath)) return;
        Process.Start(new ProcessStartInfo { FileName = _lastBackupPath, UseShellExecute = true });
    }
}

internal sealed record ScanInfo(ulong ItemId, string Title, bool Eligible, string Reason, int OriginalBytes,
    int? CandidateBytes, int? DescriptionChars, string OriginalHash, string? CandidateHash,
    string? CandidateRaw, string OriginalRaw, string BackupDirectory, ulong SteamOwner,
    bool OwnerMatches, MetadataDiagnosis Diagnosis);

internal sealed record RemoteInfo(string Raw, ulong Owner);

internal static class SteamFix
{
    private const uint AppId = 1133870;
    private const int ReadBuffer = 32768;
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static bool TryGetId(string input, out ulong id)
    {
        id = 0;
        input = input.Trim();
        if (ulong.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out id)) return id > 0;
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "steamcommunity.com" ||
            !uri.AbsolutePath.StartsWith("/sharedfiles/filedetails", StringComparison.OrdinalIgnoreCase)) return false;
        var match = Regex.Match(uri.Query, @"(?:^|[?&])id=(\d+)(?:&|$)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        return match.Success && ulong.TryParse(match.Groups[1].Value, out id) && id > 0;
    }

    internal static T WithSteam<T>(Func<T> action)
    {
        if (!SteamAPI.Init()) throw new InvalidOperationException("Steam initialization failed. Start Steam and log in, then close SE2 and ModHUB.");
        try {
            if (SteamUtils.GetAppID().m_AppId != AppId)
                throw new InvalidOperationException($"Wrong Steam application ({SteamUtils.GetAppID().m_AppId}). This tool supports only SE2 (1133870).");
            return action();
        } finally { SteamAPI.Shutdown(); }
    }

    public static ScanInfo Scan(ulong id) => WithSteam(() => ScanConnected(id));

    private static ScanInfo ScanConnected(ulong id)
    {
        RemoteInfo remote = ReadRemote(id);
        string hash = Hash(remote.Raw);
        string dir = NewBackupFolder(id);
        File.WriteAllText(Path.Combine(dir, "original-developer-metadata-PRIVATE.json"), remote.Raw, Utf8NoBom);

        PatchEvaluation patch = MetadataPatch.Evaluate(remote.Raw, id);
        MetadataDiagnosis diagnosis = UniversalRecovery.Diagnose(remote.Raw, id);
        bool ownerMatches = remote.Owner != 0 &&
            SteamUser.GetSteamID().m_SteamID != 0 &&
            remote.Owner == SteamUser.GetSteamID().m_SteamID;
        bool eligible = patch.Eligible && ownerMatches;
        string reason = ownerMatches ? patch.Reason :
            "The signed-in Steam user does not own this Workshop item. Only its owner can apply a repair.";
        string? candidate = eligible ? patch.CandidateRaw : null;
        string? candidateHash = candidate is null ? null : Hash(candidate);
        if (eligible && candidate is not null)
            File.WriteAllText(Path.Combine(dir, "candidate-metadata-PRIVATE.json"), candidate, Utf8NoBom);

        var info = new ScanInfo(id, patch.Title, eligible, reason, patch.OriginalBytes,
            patch.CandidateBytes, patch.DescriptionChars,
            hash, candidateHash, candidate, remote.Raw, dir, remote.Owner, ownerMatches, diagnosis);
        File.WriteAllText(Path.Combine(dir, "report-SAFE-TO-SHARE.json"), JsonSerializer.Serialize(new {
            appId = AppId, workshopItem = id, dateUtc = DateTimeOffset.UtcNow,
            originalBytes = patch.OriginalBytes, originalHash = hash,
            candidateBytes = patch.CandidateBytes, candidateHash,
            descriptionChars = patch.DescriptionChars,
            ownerMatches, eligible, reason, classification = diagnosis.Condition.ToString(),
            workshopDescriptionUpdated = false, contentUpdated = false
        }, new JsonSerializerOptions { WriteIndented = true }), Utf8NoBom);
        return info;
    }

    public static string Apply(ScanInfo preview) => WithSteam(() => ApplyConnected(preview));

    private static string ApplyConnected(ScanInfo preview)
    {
        if (!preview.Eligible || preview.CandidateRaw is null || preview.CandidateHash is null)
            throw new InvalidOperationException("A valid owner-only preview is required first.");
        ulong id = preview.ItemId;
        RemoteInfo now = ReadRemote(id); // Read again; abort if Steam changed after the preview.
        if (now.Owner == 0 || now.Owner != preview.SteamOwner || now.Owner != SteamUser.GetSteamID().m_SteamID)
            throw new InvalidOperationException("Steam owner changed / does not match current account. NO UPDATE SENT.");
        if (Hash(now.Raw) != preview.OriginalHash || now.Raw != preview.OriginalRaw)
            throw new InvalidOperationException("Remote metadata changed since preview. NO UPDATE SENT. Run CHECK again.");
        var recomputed = RecomputeCandidate(now.Raw, id);
        if (recomputed != preview.CandidateRaw || Hash(recomputed) != preview.CandidateHash)
            throw new InvalidOperationException("Candidate no longer matches the verified one-field repair. NO UPDATE SENT.");
        string originalBackup = Path.Combine(preview.BackupDirectory, "original-developer-metadata-PRIVATE.json");
        string candidateBackup = Path.Combine(preview.BackupDirectory, "candidate-metadata-PRIVATE.json");
        if (!File.Exists(originalBackup) || !File.Exists(candidateBackup))
            throw new InvalidOperationException("Original/candidate backup missing. NO UPDATE SENT.");
        if (File.ReadAllText(originalBackup, Utf8NoBom) != preview.OriginalRaw ||
            File.ReadAllText(candidateBackup, Utf8NoBom) != recomputed)
            throw new InvalidOperationException("A backup was modified after CHECK. NO UPDATE SENT.");

        var handle = SteamUGC.StartItemUpdate(new AppId_t(AppId), new PublishedFileId_t(id));
        if (handle == UGCUpdateHandle_t.Invalid) throw new InvalidOperationException("Steam rejected StartItemUpdate. No update submitted.");
        if (!SteamUGC.SetItemMetadata(handle, recomputed))
            throw new InvalidOperationException("Steam rejected SetItemMetadata. No update submitted.");
        var call = SteamUGC.SubmitItemUpdate(handle, "Repair oversized SE2 developer metadata (no content changes)");
        SubmitItemUpdateResult_t? response = null;
        bool ioFailure = false;
        var onDone = CallResult<SubmitItemUpdateResult_t>.Create((result, failed) => { response = result; ioFailure = failed; });
        onDone.Set(call);
        var watch = Stopwatch.StartNew();
        while (response is null && watch.Elapsed < TimeSpan.FromSeconds(120)) {
            SteamAPI.RunCallbacks();
            Thread.Sleep(100);
        }
        if (response is null) throw new TimeoutException("Steam submit timed out. Outcome UNKNOWN. Do not retry without CHECK.");
        var result = response.Value;
        File.WriteAllText(Path.Combine(preview.BackupDirectory, "steam-submit-result.txt"),
            $"Result={result.m_eResult}\nIOFailure={ioFailure}\nWorkshopId={result.m_nPublishedFileId.m_PublishedFileId}\nAgreementNeeded={result.m_bUserNeedsToAcceptWorkshopLegalAgreement}\n");
        if (ioFailure || result.m_eResult != EResult.k_EResultOK || result.m_nPublishedFileId.m_PublishedFileId != id || result.m_bUserNeedsToAcceptWorkshopLegalAgreement)
            throw new InvalidOperationException("Steam did not confirm success. Outcome needs inspection. No automatic retry.");
        var after = ReadRemote(id);
        File.WriteAllText(Path.Combine(preview.BackupDirectory, "steam-readback-after-PRIVATE.json"), after.Raw, Utf8NoBom);
        if (after.Owner != preview.SteamOwner || Hash(after.Raw) != preview.CandidateHash || after.Raw != preview.CandidateRaw)
            throw new InvalidOperationException("Steam accepted the update, but read-back did not match. State UNKNOWN. Do not retry blindly.");
        return $"SUCCESS: Steam confirmed repaired developer metadata ({Encoding.UTF8.GetByteCount(after.Raw)} bytes). Original Workshop item {id} preserved. Backup: {preview.BackupDirectory}";
    }

    private static string RecomputeCandidate(string raw, ulong id)
    {
        var patch = MetadataPatch.Evaluate(raw, id);
        if (!patch.Eligible || patch.CandidateRaw is null)
            throw new InvalidOperationException("Metadata no longer matches this known safe repair: " + patch.Reason);
        return patch.CandidateRaw;
    }

    internal static RemoteInfo ReadRemote(ulong id)
    {
        var handle = SteamUGC.CreateQueryUGCDetailsRequest(new[] { new PublishedFileId_t(id) }, 1);
        try {
            if (!SteamUGC.SetReturnMetadata(handle, true)) throw new InvalidOperationException("Steam refused SetReturnMetadata.");
            SteamUGC.SetAllowCachedResponse(handle, 0);
            SteamUGCQueryCompleted_t? finished = null;
            bool ioFailed = false;
            var callback = CallResult<SteamUGCQueryCompleted_t>.Create((data, failed) => { finished = data; ioFailed = failed; });
            callback.Set(SteamUGC.SendQueryUGCRequest(handle));
            var watch = Stopwatch.StartNew();
            while (finished is null && watch.Elapsed < TimeSpan.FromSeconds(45)) {
                SteamAPI.RunCallbacks();
                Thread.Sleep(50);
            }
            if (finished is null) throw new TimeoutException("Steam query timed out.");
            var q = finished.Value;
            if (ioFailed || q.m_eResult != EResult.k_EResultOK || q.m_unNumResultsReturned != 1 || q.m_bCachedData)
                throw new InvalidOperationException($"Steam query did not return fresh data ({q.m_eResult}; cached={q.m_bCachedData}).");
            if (!SteamUGC.GetQueryUGCResult(handle, 0, out SteamUGCDetails_t details) ||
                details.m_eResult != EResult.k_EResultOK || details.m_nPublishedFileId.m_PublishedFileId != id ||
                details.m_nConsumerAppID.m_AppId != AppId)
                throw new InvalidOperationException("Steam returned unexpected Workshop item.");
            // Successful Steam API calls returning empty metadata are NOT deleted items.
            // An unsuccessful metadata query is still an error, not a missing-data state.
            if (!SteamUGC.GetQueryUGCMetadata(handle, 0, out string raw, ReadBuffer))
                throw new InvalidOperationException("Steam metadata retrieval failed (not equivalent to empty developer metadata).");
            return new RemoteInfo(raw ?? "", details.m_ulSteamIDOwner);
        } finally { SteamUGC.ReleaseQueryUGCRequest(handle); }
    }

    internal static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    internal static string NewBackupFolder(ulong id)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SE2WorkshopUGCFix", "Backups", id + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
