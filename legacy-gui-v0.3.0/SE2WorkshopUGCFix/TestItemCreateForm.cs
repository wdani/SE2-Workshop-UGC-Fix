using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SE2WorkshopUGCFix;

internal sealed class TestItemCreateForm : Form
{
    private readonly TextBox _confirmation = new() { Dock = DockStyle.Top };
    private readonly Button _submit = new() {
        Text = "CREATE ONE EMPTY TEST ITEM", Dock = DockStyle.Top, Height = 44, Enabled = false
    };
    private readonly TextBox _result = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true };
    private bool _busy;
    internal ulong? CreatedItemId { get; private set; }

    internal TestItemCreateForm()
    {
        Text = "Disposable SE2 Workshop item";
        ClientSize = new Size(720, 460);
        MinimumSize = new Size(640, 420);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10F);
        var top = new Panel { Dock = DockStyle.Top, Height = 240, Padding = new Padding(14) };
        var intro = new Label {
            Dock = DockStyle.Top, Height = 132,
            Text = "This CREATES a new Workshop item on your Steam account, not an existing mod. " +
                   "No content or developer metadata is submitted. Visibility is not verified and " +
                   "must NOT be assumed private. Steam may not return the new empty item in queries. " +
                   "One attempt only; an uncertain result must NEVER be retried automatically.\r\n\r\n" +
                   "Type exactly: " + TestFixturePolicy.Confirmation
        };
        top.Controls.Add(_submit);
        top.Controls.Add(_confirmation);
        top.Controls.Add(intro);
        Controls.Add(_result);
        Controls.Add(top);
        _confirmation.TextChanged += (_, _) => _submit.Enabled = !_busy &&
            TestFixturePolicy.MayCreate(_confirmation.Text, File.Exists(TestFixturePolicy.LedgerPath));
        _submit.Click += async (_, _) => {
            if (_busy || !TestFixturePolicy.MayCreate(_confirmation.Text,
                    File.Exists(TestFixturePolicy.LedgerPath))) return;
            if (MessageBox.Show(this,
                "Create ONE real Workshop test item? Its visibility has not been verified.\n" +
                "Existing mods will not be changed. This action changes your Steam account.",
                "Confirm Steam item creation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            _busy = true;
            _submit.Enabled = false;
            _confirmation.Enabled = false;
            try {
                var created = await Task.Run(() => TestItemCreator.Create(_confirmation.Text));
                CreatedItemId = created.ItemId;
                _result.Text = "CREATED: " + created.ItemId + Environment.NewLine +
                    "Result: " + created.SteamResult + Environment.NewLine +
                    "Agreement needed: " + created.NeedsWorkshopAgreement + Environment.NewLine +
                    "Receipt: " + created.ReceiptPath + Environment.NewLine +
                    "No metadata/content sent. Visibility remains unverified.";
            }
            catch (Exception e) {
                _result.Text = "STOP: " + e.Message + Environment.NewLine +
                    "Review the local TestFixture ledger before any further creation.";
            }
            finally { _busy = false; }
        };
        FormClosing += (_, e) => { if (_busy) e.Cancel = true; };
        if (File.Exists(TestFixturePolicy.LedgerPath))
            _result.Text = "Previous item creation attempt exists. Creation locked. " +
                "Review " + TestFixturePolicy.LedgerPath;
    }
}
