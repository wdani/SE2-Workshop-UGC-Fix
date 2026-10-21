using System.Windows.Forms;

namespace SE2WorkshopUGCFix;

internal sealed class PreflightForm : Form
{
    private readonly TextBox _description = new() {
        Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical,
        MaxLength = 100_000, AcceptsReturn = true, AcceptsTab = false,
        PlaceholderText = "Paste the planned public Workshop description here (BBCode, emojis, text)..."
    };
    private readonly TextBox _result = new() {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill, BackColor = System.Drawing.Color.FromArgb(247, 248, 251)
    };
    private readonly string? _template;
    private readonly Label _status = new() {
        AutoSize = true, ForeColor = System.Drawing.Color.FromArgb(60, 75, 100),
        Text = "OFFLINE ESTIMATE ONLY — no Steam changes, no uploads."
    };
    private readonly Button _copy = new() { Text = "COPY RESULT", Height = 36, Enabled = false };

    public PreflightForm(string? template, ulong? itemId)
    {
        _template = template;
        Text = "SE2 Workshop UGC Fix - Description Preflight";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new System.Drawing.Size(730, 600);
        MinimumSize = new System.Drawing.Size(630, 520);
        Font = new System.Drawing.Font("Segoe UI", 10);
        var layout = new TableLayoutPanel {
            Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 7
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        Controls.Add(layout);
        layout.Controls.Add(new Label {
            Text = "PRE-PUBLISH DESCRIPTION CHECK", Dock = DockStyle.Fill,
            Font = new System.Drawing.Font("Segoe UI", 17, System.Drawing.FontStyle.Bold)
        }, 0, 0);
        layout.Controls.Add(new Label {
            Text = "A long public description ALONE does not cause the bug. This estimates what the INTERNAL 5,000-byte Steam developer-metadata field might reach IF ModHUB also embeds this description. Scan your item first for a closer estimate.",
            Dock = DockStyle.Fill, ForeColor = System.Drawing.Color.FromArgb(78, 80, 90)
        }, 0, 1);
        layout.Controls.Add(_description, 0, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        var calculate = new Button { Text = "ESTIMATE RISK", AutoSize = true, Height = 35 };
        calculate.Click += (_, _) => RunEstimate();
        _copy.Click += (_, _) => Clipboard.SetText(_result.Text);
        var clear = new Button { Text = "CLEAR TEXT", AutoSize = true, Height = 35 };
        clear.Click += (_, _) => { _description.Clear(); _result.Clear(); _copy.Enabled = false; };
        buttons.Controls.Add(calculate);
        buttons.Controls.Add(_copy);
        buttons.Controls.Add(clear);
        layout.Controls.Add(buttons, 0, 3);
        layout.Controls.Add(_result, 0, 4);
        layout.Controls.Add(_status, 0, 5);
        layout.Controls.Add(new Label {
            Text = itemId is null ? "No Steam item scanned yet — numbers are reference estimates only."
                : $"Template: last read-only scan of Workshop item {itemId}. No live writes.",
            Dock = DockStyle.Fill, ForeColor = System.Drawing.Color.Gray,
            Font = new System.Drawing.Font("Segoe UI", 8.5F)
        }, 0, 6);
        AcceptButton = calculate;
    }

    private void RunEstimate()
    {
        var result = DescriptionPreflight.Calculate(_description.Text, _template);
        _result.Text = $"Characters (UTF-16): {result.DescriptionChars:N0}\r\n" +
            $"Description UTF-8 bytes: {result.DescriptionUtf8Bytes:N0}\r\n" +
            $"JSON-encoded description size: {result.JsonMinBytes:N0}–{result.JsonConservativeBytes:N0} bytes\r\n" +
            $"Baseline developer metadata (excluding description token): {result.BaseBytesMin:N0} bytes\r\n" +
            $"PROJECTED internal metadata IF description is embedded: {result.ProjectedMin:N0}–{result.ProjectedConservative:N0} bytes\r\n" +
            $"Steam developer metadata limit: {MetadataPatch.LimitBytes:N0} bytes\r\n\r\n" +
            $"{result.RiskLabel}\r\n\r\n{result.TemplateNote}\r\n\r\n" +
            "This is NOT a measurement of an unpublished item or a guarantee of a safe upload. " +
            "Actual ModHUB JSON, tags, paths, and escaping may change. After each test publication " +
            "run CHECK METADATA on the disposable item to measure the real size. " +
            "If the text is only on the public Workshop page, this 5,000-byte limit does not apply to it.";
        _status.Text = result.ProjectedConservative >= MetadataPatch.LimitBytes
            ? "WARNING: possible oversized internal metadata. Do not risk an important published mod."
            : "Estimated only. Use a disposable mod and check Steam metadata after publishing.";
        _status.ForeColor = result.ProjectedConservative >= MetadataPatch.LimitBytes
            ? System.Drawing.Color.DarkRed : System.Drawing.Color.DarkGreen;
        _copy.Enabled = true;
    }
}
