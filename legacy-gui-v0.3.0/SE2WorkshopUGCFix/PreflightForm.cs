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
        Dock = DockStyle.Fill, BackColor = ModernUi.Surface
    };
    private readonly string? _template;
    private readonly Label _status = new() {
        AutoSize = false, Dock = DockStyle.Fill, ForeColor = ModernUi.Muted,
        Text = "OFFLINE ESTIMATE ONLY — no Steam changes, no uploads."
    };
    private readonly Button _copy = new() { Text = "COPY RESULT", Height = 36, Enabled = false };

    public PreflightForm(string? template, ulong? itemId)
    {
        _template = template;
        Text = "SE2 Workshop UGC Fix - Description Preflight";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new System.Drawing.Size(890, 750);
        MinimumSize = new System.Drawing.Size(760, 650);
        ModernUi.Window(this);
        var layout = new TableLayoutPanel {
            Dock = DockStyle.Fill, Padding = new Padding(22),
            BackColor = ModernUi.Background, ColumnCount = 1, RowCount = 7
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        Controls.Add(layout);
        layout.Controls.Add(ModernUi.Label(
            "DESCRIPTION   /   SIZE ESTIMATOR", 18F, true), 0, 0);
        layout.Controls.Add(ModernUi.Label(
            "An oversized public description alone is not the bug. Estimate how " +
            "ModHUB might embed it in Steam's 5,000-byte INTERNAL developer metadata.",
            10F, false, ModernUi.Muted), 0, 1);
        ModernUi.Input(_description, multiline: true);
        var descriptionCard = ModernUi.Card(14);
        descriptionCard.Controls.Add(_description);
        layout.Controls.Add(descriptionCard, 0, 2);
        var buttons = new FlowLayoutPanel {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, Padding = new Padding(0, 4, 0, 2)
        };
        var calculate = new Button { Text = "ESTIMATE RISK", Width = 192, Height = 40 };
        ModernUi.Button(calculate, primary: true);
        calculate.Click += (_, _) => RunEstimate();
        _copy.Click += (_, _) => Clipboard.SetText(_result.Text);
        var clear = new Button { Text = "CLEAR TEXT", Width = 136, Height = 40 };
        ModernUi.Button(clear);
        _copy.Width = 156;
        _copy.Height = 40;
        ModernUi.Button(_copy);
        clear.Click += (_, _) => { _description.Clear(); _result.Clear(); _copy.Enabled = false; };
        buttons.Controls.Add(calculate);
        buttons.Controls.Add(_copy);
        buttons.Controls.Add(clear);
        layout.Controls.Add(buttons, 0, 3);
        ModernUi.Input(_result, multiline: true);
        _result.BackColor = ModernUi.Surface;
        var resultCard = ModernUi.Card(13);
        resultCard.Controls.Add(_result);
        layout.Controls.Add(resultCard, 0, 4);
        layout.Controls.Add(_status, 0, 5);
        layout.Controls.Add(new Label {
            Text = itemId is null ? "No Steam item scanned yet — numbers are reference estimates only."
                : $"Template: last read-only scan of Workshop item {itemId}. No live writes.",
            Dock = DockStyle.Fill, ForeColor = ModernUi.Muted,
            Font = new System.Drawing.Font("Segoe UI", 9F)
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
            ? ModernUi.Danger : ModernUi.Success;
        _copy.Enabled = true;
    }
}
