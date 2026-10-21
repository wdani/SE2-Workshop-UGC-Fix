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
        ClientSize = new Size(830, 615);
        MinimumSize = new Size(730, 540);
        Font = new Font("Segoe UI", 10F);
        var panel = new TableLayoutPanel {
            Dock = DockStyle.Fill, Padding = new Padding(18),
            ColumnCount = 1, RowCount = 8
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (int n in new[] { 52, 106, 34, 46, 46, 42, 60 })
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, n));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
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
                "The public title, description and content are not set by this tool. " +
                "Steam failures can leave the test item damaged; the original files are saved first."
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
            Text = "Workshop ID " + _id + " — type EXACTLY: RUN FULL E2E " + _id
        }, 0, 4);
        panel.Controls.Add(_confirmation, 0, 5);
        panel.Controls.Add(_run, 0, 6);
        panel.Controls.Add(_log, 0, 7);
        void UpdateAvailability() => _run.Enabled = !_busy &&
            File.Exists(_path.Text) &&
            _confirmation.Text == "RUN FULL E2E " + _id;
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
                        if (IsHandleCreated) BeginInvoke(() =>
                            _log.AppendText(stage + Environment.NewLine));
                    }));
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
        FormClosing += (_, e) => { if (_busy) e.Cancel = true; };
    }
}
