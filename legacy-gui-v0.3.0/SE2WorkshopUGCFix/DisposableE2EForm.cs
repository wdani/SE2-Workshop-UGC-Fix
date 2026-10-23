// A single confirmed Windows E2E run against a genuinely published, disposable
// ModHUB project. The full suite backs up, mutates, diagnoses and restores.
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SE2WorkshopUGCFix;

internal sealed class DisposableE2EForm : Form
{
    private readonly ulong _id;
    private readonly TextBox _path = new() { Dock = DockStyle.Fill };
    private readonly TextBox _confirmation = new() { Dock = DockStyle.Fill };
    private readonly Button _run = new() { Text = "RUN FULL DISPOSABLE MOD TEST", Dock = DockStyle.Fill, Enabled = false };
    private readonly Button _single = new() {
        Text = "PREPARE ONE GUI RESTORE TEST (BACKUP FIRST)",
        Dock = DockStyle.Fill, Enabled = false
    };
    private readonly Button _copy = new() { Text = "COPY FULL E2E SAFE REPORT", Dock = DockStyle.Fill, Enabled = false };
    private readonly TextBox _rescuePhrase = new() {
        Dock = DockStyle.Fill, PlaceholderText = "If needed: RESTORE TEST <Workshop-ID>"
    };
    private readonly Button _rescue = new() {
        Text = "EMERGENCY RESTORE FROM TEST BACKUP", Dock = DockStyle.Fill, Enabled = false
    };
    private string? _reportPath;
    private readonly TextBox _log = new() {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
        ScrollBars = ScrollBars.Both
    };
    private bool _busy;

    internal DisposableE2EForm(ulong workshopId)
    {
        _id = workshopId;
        Text = "Full E2E - real disposable SE2 ModHUB mod";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(830, 785);
        MinimumSize = new Size(730, 740);
        Font = new Font("Segoe UI", 10F);
        var panel = new TableLayoutPanel {
            Dock = DockStyle.Fill, Padding = new Padding(18),
            ColumnCount = 1, RowCount = 12
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (int n in new[] { 52, 106, 34, 46, 46, 42, 60 })
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, n));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(panel);
        panel.Controls.Add(new Label {
            Dock = DockStyle.Fill, Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            Text = "Real ModHUB mod: one complete failure/recovery test"
        }, 0, 0);
        panel.Controls.Add(new Label {
            Dock = DockStyle.Fill,
            Text = "Only a published, intentionally disposable SE2 mod named SE2 UGC E2E TEST* " +
                "is accepted. This WILL change its developer metadata repeatedly on Steam, " +
                "then try to restore the exact backup after EACH simulated fault. " +
                "For the normal RESTORE button, choose PREPARE ONE GUI RESTORE TEST " +
                "instead: it writes just one known empty state and waits for manual restoration. " +
                "The public title, description and content are not set by this tool. " +
                "Steam failures can leave the test item damaged; original files are saved first."
        }, 0, 1);
        panel.Controls.Add(new Label {
            Dock = DockStyle.Fill, Text = "Original .vrgproj belonging to this published ModHUB test mod:"
        }, 0, 2);
        var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 83));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17));
        var browse = new Button { Text = "BROWSE", Dock = DockStyle.Fill };
        browse.Click += (_, _) => {
            using var dialog = new OpenFileDialog {
                Filter = "VRAGE3 mod project (*.vrgproj)|*.vrgproj|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog(this) == DialogResult.OK) _path.Text = dialog.FileName;
        };
        pathRow.Controls.Add(_path, 0, 0);
        pathRow.Controls.Add(browse, 1, 0);
        panel.Controls.Add(pathRow, 0, 3);
        panel.Controls.Add(new Label {
            Dock = DockStyle.Fill,
            Text = "Workshop " + _id + ": RUN FULL E2E " + _id +
                "  or  PREPARE SINGLE RESTORE " + _id
        }, 0, 4);
        panel.Controls.Add(_confirmation, 0, 5);
        panel.Controls.Add(_run, 0, 6);
        panel.Controls.Add(_single, 0, 7);
        panel.Controls.Add(_log, 0, 8);
        panel.Controls.Add(_copy, 0, 9);
        panel.Controls.Add(_rescuePhrase, 0, 10);
        panel.Controls.Add(_rescue, 0, 11);
        _rescuePhrase.TextChanged += (_, _) =>
            _rescue.Enabled = !_busy && _rescuePhrase.Text == "RESTORE TEST " + _id;
        _rescue.Click += async (_, _) => {
            if (_busy || _rescuePhrase.Text != "RESTORE TEST " + _id) return;
            string pointer = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SE2WorkshopUGCFix", "e2e-latest-backup-" + _id + ".txt");
            if (!File.Exists(pointer)) {
                MessageBox.Show(this, "No local test backup pointer found. No Steam write.");
                return;
            }
            string directory = File.ReadAllText(pointer).Trim();
            if (MessageBox.Show(this,
                "Restore ONLY the known injected fault on TEST Workshop item " + _id +
                " from the backed-up original? An unknown Steam state will be rejected.",
                "Confirm emergency test rollback", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            _busy = true;
            _rescue.Enabled = false;
            try {
                string outcome = await Task.Run(() =>
                    DisposableE2EManualRescue.Restore(_id, directory, _rescuePhrase.Text));
                _log.AppendText(outcome + Environment.NewLine);
            } catch (Exception ex) {
                _log.AppendText("EMERGENCY RESTORE REFUSED: " + ex.Message +
                    Environment.NewLine);
            } finally { _busy = false; }
        };
        _copy.Click += (_, _) => {
            if (_reportPath is not null && File.Exists(_reportPath)) {
                Clipboard.SetText(File.ReadAllText(_reportPath));
                _log.AppendText("Safe consolidated report copied to clipboard." + Environment.NewLine);
            }
        };
        void UpdateAvailability() {
            bool projectExists = !_busy && File.Exists(_path.Text);
            _run.Enabled = projectExists && _confirmation.Text == "RUN FULL E2E " + _id;
            _single.Enabled = projectExists &&
                _confirmation.Text == "PREPARE SINGLE RESTORE " + _id;
        }
        _path.TextChanged += (_, _) => UpdateAvailability();
        _confirmation.TextChanged += (_, _) => UpdateAvailability();

        _run.Click += async (_, _) => {
            if (_busy || !File.Exists(_path.Text) ||
                _confirmation.Text != "RUN FULL E2E " + _id) return;
            if (MessageBox.Show(this,
                "This will WRITE malformed, mismatched and empty metadata on ONLY the " +
                "disposable Workshop item " + _id + ", with a backed-up restore after each case.\n\n" +
                "If Steam fails mid-update the test item may remain broken, requiring manual repair.\n\n" +
                "Do you authorize this real Steam E2E test?",
                "FINAL CONSENT - Steam metadata will change",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            _busy = true;
            _run.Enabled = false;
            browse.Enabled = false;
            _path.Enabled = false;
            _confirmation.Enabled = false;
            _log.AppendText("E2E started for disposable item " + _id + Environment.NewLine);
            try {
                var result = await Task.Run(() =>
                    DisposableE2ERunService.Run(_id, _path.Text, stage => {
                        if (IsHandleCreated) BeginInvoke(new Action(() =>
                            _log.AppendText(stage + Environment.NewLine)));
                    }));
                _reportPath = result.SummaryFile;
                _copy.Enabled = File.Exists(result.SummaryFile);
                _log.AppendText("RESULT: " + (result.Complete ? "PASS" : "PARTIAL / STOP") +
                    Environment.NewLine + "Original restored: " + result.OriginalRestored +
                    Environment.NewLine + "Safe report: " + result.SummaryFile +
                    Environment.NewLine);
                foreach (var step in result.Steps)
                    _log.AppendText(step.Scenario + ": " + step.Status + " — " +
                        step.Detail + Environment.NewLine);
                if (!result.OriginalRestored)
                    MessageBox.Show(this, "STOP! Original Workshop metadata is NOT confirmed restored. " +
                        "Keep the private backup and do not republish or retry automatically.",
                        "Manual inspection required", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } catch (Exception error) {
                _log.AppendText("STOP: " + error.Message + Environment.NewLine);
                MessageBox.Show(this, "Test aborted: " + error.Message +
                    "\nInspect the Workshop test item and the private backup; no blind retry.",
                    "E2E test stopped", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            } finally { _busy = false; }
        };
        _single.Click += async (_, _) => {
            if (_busy || !_single.Enabled ||
                _confirmation.Text != "PREPARE SINGLE RESTORE " + _id) return;
            if (MessageBox.Show(this,
                "This will intentionally set INTERNAL developer metadata to EMPTY on ONLY the " +
                "published disposable SE2 UGC E2E TEST item " + _id + ".\n\n" +
                "The tool SAVES original metadata and project BEFORE the single Steam update. " +
                "It will NOT restore automatically: use the NORMAL app's RECOVERY PREVIEW + " +
                "RESTORE button and select the saved original JSON.\n\n" +
                "If anything fails, use EMERGENCY RESTORE FROM TEST BACKUP here. " +
                "Do NOT touch production mods or existing worlds.\n\n" +
                "Authorize this ONE real Steam metadata mutation?",
                "FINAL CONSENT — prepare single disposable restore",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            _busy = true;
            string selectedProject = _path.Text;
            _run.Enabled = false;
            _single.Enabled = false;
            browse.Enabled = false;
            _path.Enabled = false;
            _confirmation.Enabled = false;
            _log.AppendText("Single GUI restore preparation started for disposable item " +
                _id + Environment.NewLine);
            try {
                var item = await Task.Run(() =>
                    DisposableSingleRestoreSetup.Prepare(_id, selectedProject));
                _log.AppendText(
                    "PASS: Exactly one empty developer-metadata state confirmed on Steam." +
                    Environment.NewLine +
                    "ORIGINAL BACKUP FOR NORMAL RECOVERY PREVIEW:" + Environment.NewLine +
                    item.OriginalBackupFile + Environment.NewLine +
                    "Original SHA256: " + item.OriginalSha256 + Environment.NewLine +
                    "Close this fixture lab. Open RUN-APP.cmd, CHECK METADATA " + _id +
                    ", open RECOVERY PREVIEW, choose the original backup above, then approve RESTORE " +
                    _id + "." + Environment.NewLine +
                    "If the normal restore fails, do NOT repeat preparation: return here and " +
                    "use EMERGENCY RESTORE FROM TEST BACKUP." + Environment.NewLine);
                MessageBox.Show(this,
                    "Single disposable fault is ready. Original was backed up. " +
                    "Run the ordinary RESTORE procedure now; never run preparation twice.",
                    "Prepared: finish via normal GUI", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            } catch (Exception ex) {
                _log.AppendText("PREPARATION STOP / possibly unknown Steam state: " +
                    ex.Message + Environment.NewLine);
                MessageBox.Show(this, ex.Message +
                    "\n\nDO NOT retry a Steam mutation blindly. Preserve the original backup.",
                    "Preparation stopped", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            } finally {
                _busy = false;
                browse.Enabled = true;
                _path.Enabled = true;
                _confirmation.Enabled = true;
                UpdateAvailability();
            }
        };
        FormClosing += (_, e) => { if (_busy) e.Cancel = true; };
    }
}
