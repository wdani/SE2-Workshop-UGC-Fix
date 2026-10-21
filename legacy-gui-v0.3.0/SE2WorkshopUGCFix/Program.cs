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
    [STAThread]
    private static int Main(string[] args)
    {
        // The offline UI smoke mode never initializes Steam and cannot submit updates.
        // Exit codes make this executable usable by a Windows test runner.
        if (args.Length == 1 && args[0] == "--smoke-ui")
        {
            try
            {
                ApplicationConfiguration.Initialize();
                using var main = new FixForm();
                using var recovery = new RecoveryPreviewForm(12345678UL);
                main.ShowInTaskbar = false;
                main.Opacity = 0;
                main.Show();
                Application.DoEvents();
                var controls = Descendants(main).OfType<Button>().ToArray();
                var repair = controls.Single(c => c.Text.Contains("REPAIR", StringComparison.OrdinalIgnoreCase));
                var preview = controls.Single(c => c.Text == "RECOVERY PREVIEW");
                var scan = controls.Single(c => c.Text.Contains("CHECK METADATA", StringComparison.OrdinalIgnoreCase));
                var probe = controls.Single(c => c.Text == "READ-ONLY UGC PROBE");
                var fixture = controls.Single(c => c.Text == "CREATE TEST ITEM");
                if (repair.Enabled || preview.Enabled || !scan.Enabled || !probe.Enabled || !fixture.Enabled)
                    throw new InvalidOperationException("Unsafe initial GUI button state.");
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
                if (!recovery.Text.Contains("Recovery Preview", StringComparison.Ordinal))
                    throw new InvalidOperationException("Recovery preview form failed to initialize.");
                recovery.ShowInTaskbar = false;
                recovery.Opacity = 0;
                recovery.Show();
                Application.DoEvents();
                if (!Descendants(recovery).OfType<Button>()
                    .Any(c => c.Text == "BUILD READ-ONLY RECOVERY PREVIEW"))
                    throw new InvalidOperationException("Recovery preview button missing.");
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
    private readonly Button _fixture = new() { Text = "CREATE TEST ITEM", Dock = DockStyle.Fill, Enabled = true };
    private readonly Button _probe = new() { Text = "READ-ONLY UGC PROBE", Dock = DockStyle.Fill,
        Font = new System.Drawing.Font("Segoe UI", 9F) };
    private readonly Button _repair = new() { Text = "2   REPAIR MY ITEM", Height = 44, Dock = DockStyle.Fill, Enabled = false };
    private readonly Button _recover = new() { Text = "RECOVERY PREVIEW", Height = 36, Dock = DockStyle.Fill, Enabled = false };
    private readonly Button _preflight = new() { Text = "DESCRIPTION PREFLIGHT", Height = 36, Dock = DockStyle.Fill };
    private readonly Button _report = new() { Text = "COPY SAFE REPORT", Height = 36, Dock = DockStyle.Fill, Enabled = false };
    private readonly Button _backup = new() { Text = "OPEN BACKUP FOLDER", Height = 36, Dock = DockStyle.Fill, Enabled = false };
    private readonly Label _status = new() { AutoSize = true, Text = "Ready. Steam must be running; close SE2 and ModHUB." };
    private readonly Label _stats = new() { AutoSize = true, Text = "No item inspected yet." };
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = System.Drawing.Color.FromArgb(245, 247, 250), BorderStyle = BorderStyle.FixedSingle };
    private ScanInfo? _current;
    private string? _lastBackupPath;
    private bool _busy;

    public FixForm()
    {
        Text = "SE2 Workshop UGC Metadata Fix   •   v0.4.0-preview.3 (READ-ONLY RECOVERY)";
        // Reserve actual vertical space for the grouped actions and security note.
        // The previous 33px toolbar row clipped buttons at normal Windows DPI.
        ClientSize = new System.Drawing.Size(880, 800);
        MinimumSize = new System.Drawing.Size(760, 735);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new System.Drawing.Font("Segoe UI", 10F);
        BackColor = System.Drawing.Color.White;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 10 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(layout);

        var title = new Label { Text = "Workshop metadata recovery", Dock = DockStyle.Fill, Font = new System.Drawing.Font("Segoe UI", 19F, System.Drawing.FontStyle.Bold) };
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(new Label {
            Text = "For Space Engineers 2 (App 1133870). Detects oversized, empty, malformed, or healthy metadata. Existing-item changes are limited to the proven description patch; missing-data recovery is PREVIEW ONLY.",
            Dock = DockStyle.Fill, AutoSize = false, ForeColor = System.Drawing.Color.FromArgb(70, 80, 92)
        }, 0, 1);
        layout.Controls.Add(_workshop, 0, 2);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0) };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _scan.Margin = new Padding(0, 4, 8, 6);
        _repair.Margin = new Padding(8, 4, 0, 6);
        _scan.BackColor = System.Drawing.Color.FromArgb(33, 101, 168);
        _scan.ForeColor = System.Drawing.Color.White;
        _scan.FlatStyle = FlatStyle.Flat;
        _repair.UseVisualStyleBackColor = false;
        _repair.BackColor = System.Drawing.Color.FromArgb(218, 224, 232);
        _repair.ForeColor = System.Drawing.Color.FromArgb(88, 95, 103);
        _repair.FlatStyle = FlatStyle.Flat;
        actions.Controls.Add(_scan, 0, 0);
        actions.Controls.Add(_repair, 1, 0);
        layout.Controls.Add(actions, 0, 3);
        // Explicit diagnostic can run even when ordinary CHECK METADATA fails.
        // It never enables repair and does not submit any Steam changes.
        var statusBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Padding = new Padding(0), Margin = new Padding(0) };
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 73));
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        var diagnosticButtons = new TableLayoutPanel {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Margin = new Padding(7, 2, 0, 2)
        };
        diagnosticButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        diagnosticButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        diagnosticButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        foreach (var action in new[] { _probe, _fixture }) {
            action.Margin = new Padding(0, 2, 0, 2);
            action.FlatStyle = FlatStyle.Flat;
            action.ForeColor = System.Drawing.Color.FromArgb(35, 88, 140);
            action.BackColor = System.Drawing.Color.FromArgb(246, 249, 253);
            diagnosticButtons.Controls.Add(action);
        }
        statusBar.Controls.Add(_status, 0, 0);
        statusBar.Controls.Add(diagnosticButtons, 1, 0);
        layout.Controls.Add(statusBar, 0, 4);
        layout.Controls.Add(_stats, 0, 5);
        var caption = new Label { Text = "Activity / diagnostic log", Dock = DockStyle.Fill, Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold) };
        layout.Controls.Add(caption, 0, 6);
        layout.Controls.Add(_log, 0, 7);
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

        layout.Controls.Add(new Label {
            Text = "No passwords or automatic publishing. Steam writes require item ownership and explicit confirmation. Keep your backups private.",
            Dock = DockStyle.Fill, AutoSize = false, TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Padding = new Padding(3, 3, 0, 0), Margin = new Padding(0),
            ForeColor = System.Drawing.Color.FromArgb(88, 98, 111),
            Font = new System.Drawing.Font("Segoe UI", 9F)
        }, 0, 9);
        _scan.Click += async (_, _) => await ScanAsync();
        _probe.Click += async (_, _) => await ProbeAsync();
        _fixture.Click += (_, _) => {
            if (_busy) return;
            using var wizard = new TestItemCreateForm();
            wizard.ShowDialog(this);
            if (wizard.CreatedItemId is ulong id) {
                _workshop.Text = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _status.Text = "New test-item ID entered. Use READ-ONLY UGC PROBE; do NOT REPAIR.";
                _status.ForeColor = System.Drawing.Color.DarkOrange;
                Append("Disposable Workshop test item created: " + id +
                    ". No content or metadata uploaded. Visibility not yet verified.");
            }
        };
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
            _status.ForeColor = System.Drawing.Color.FromArgb(47, 65, 83);
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
        Append("This version classifies missing and malformed metadata and PREVIEWS recovery. Only the original oversized-description patch can change Steam.");
        Append("If metadata is invalid JSON, too short, belongs to someone else, or has an unfamiliar structure, it will refuse to update.");
    }

    private static GroupBox CreateActionGroup(string title, Button first, Button second)
    {
        var group = new GroupBox {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 13, 12, 10),
            Margin = new Padding(3, 0, 7, 0),
            ForeColor = System.Drawing.Color.FromArgb(61, 80, 100),
            Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
        };
        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        foreach (var button in new[] { first, second }) {
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(2, 3, 2, 3);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(184, 196, 210);
            button.BackColor = System.Drawing.Color.FromArgb(247, 250, 253);
            button.ForeColor = System.Drawing.Color.FromArgb(37, 77, 117);
            button.UseVisualStyleBackColor = false;
            button.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Regular);
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
                ? System.Drawing.Color.DarkGreen : System.Drawing.Color.DarkOrange;
            Append(info.Assessment.Explanation);
            Append("Safe report available via COPY SAFE REPORT. No Steam update was sent.");
        } catch (Exception ex) {
            _status.Text = "READ-ONLY PROBE FAILED — Steam data unchanged.";
            _status.ForeColor = System.Drawing.Color.DarkRed;
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
                _status.ForeColor = System.Drawing.Color.DarkOrange;
            } else {
                switch (info.Diagnosis.Condition) {
                    case MetadataCondition.Healthy:
                        _status.Text = "HEALTHY — Metadata is valid. No repair necessary.";
                        _status.ForeColor = System.Drawing.Color.DarkGreen;
                        break;
                    case MetadataCondition.OversizedDescription when info.Eligible:
                        _status.Text = "REPAIR AVAILABLE — Oversized internal description detected.";
                        _status.ForeColor = System.Drawing.Color.DarkOrange;
                        break;
                    case MetadataCondition.Missing:
                    case MetadataCondition.Malformed:
                        _status.Text = "RECOVERY PREVIEW AVAILABLE — No Steam changes will be made.";
                        _status.ForeColor = System.Drawing.Color.DarkOrange;
                        break;
                    case MetadataCondition.IdentityConflict:
                        _status.Text = "UNSAFE METADATA IDENTITY — All repairs blocked.";
                        _status.ForeColor = System.Drawing.Color.DarkRed;
                        break;
                    default:
                        _status.Text = "NO SAFE AUTOMATIC REPAIR — See diagnosis.";
                        _status.ForeColor = System.Drawing.Color.DarkOrange;
                        break;
                }
            }
        } catch (Exception ex) {
            _status.Text = "CHECK FAILED – nothing was changed on Steam.";
            _status.ForeColor = System.Drawing.Color.DarkRed;
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
            _status.ForeColor = System.Drawing.Color.DarkGreen;
            MessageBox.Show(this,
                "Steam accepted the metadata repair and a fresh read-back matched the candidate.\n\nRestart ModHUB and Space Engineers 2, then test in a disposable world. Do not overwrite existing saves until tested.",
                "Repair confirmed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        } catch (Exception ex) {
            _status.Text = "UPDATE NOT CONFIRMED – inspect activity / backup before retrying.";
            _status.ForeColor = System.Drawing.Color.DarkRed;
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
        _fixture.Enabled = !active;
        _preflight.Enabled = !active;
        _recover.Enabled = !active && _current is { OwnerMatches: true } &&
            (_current.Diagnosis.Condition is MetadataCondition.Missing or MetadataCondition.Malformed);
        _workshop.Enabled = !active;
        UpdateRepairAvailability();
        if (status is not null) { _status.Text = status; _status.ForeColor = System.Drawing.Color.FromArgb(47, 65, 83); }
        Cursor = active ? Cursors.WaitCursor : Cursors.Default;
    }

    private void UpdateRepairAvailability()
    {
        bool ready = !_busy && _current is { Eligible: true };
        _repair.Enabled = ready;
        _repair.Text = ready ? "2   REPAIR MY ITEM" : "2   REPAIR (CHECK FIRST)";
        _repair.BackColor = ready ? System.Drawing.Color.FromArgb(24, 115, 78) : System.Drawing.Color.FromArgb(218, 224, 232);
        _repair.ForeColor = ready ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(88, 95, 103);
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
