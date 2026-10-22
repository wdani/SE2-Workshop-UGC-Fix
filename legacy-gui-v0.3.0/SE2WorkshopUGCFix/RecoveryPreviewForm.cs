// Owner-only recovery: the preview is read-only; a separate explicitly approved
// APPLY operation may send ONE metadata-only update after fresh safety checks.
using Steamworks;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace SE2WorkshopUGCFix;

internal sealed record RecoveryPreviewResult(
    string SafeSummary, string BackupFolder, VerifiedRecoveryPreview Session,
    string? ProjectSourceSha256, string? LocalSourceSha256);

internal static class RecoveryPreviewService
{
    internal static RecoveryPreviewResult Prepare(
        ulong targetId, string backupJsonPath, string vrgprojPath,
        string modhubPath, string comparisonInput)
        => SteamFix.WithSteam(() => PrepareConnected(targetId,
            backupJsonPath, vrgprojPath, modhubPath, comparisonInput));

    private static RecoveryPreviewResult PrepareConnected(
        ulong targetId, string backupJsonPath, string vrgprojPath,
        string modhubPath, string comparisonInput)
    {
        ulong owner = SteamUser.GetSteamID().m_SteamID;
        if (owner == 0) throw new InvalidOperationException("Steam user not signed in.");

        // Record the initial successful, uncached Steam response even if user
        // files are bad. SteamFix.ReadRemote throws on failed/cached/wrong-app queries.
        RemoteInfo initiallyRead = SteamFix.ReadRemote(targetId);
        if (initiallyRead.Owner != owner)
            throw new InvalidOperationException("The signed-in account does not own the original item.");
        MetadataDiagnosis diagnosis = UniversalRecovery.Diagnose(initiallyRead.Raw, targetId);
        if (diagnosis.Condition is not (MetadataCondition.Missing or MetadataCondition.Malformed))
            throw new InvalidOperationException("Recovery preview not available: " + diagnosis.Condition);

        string dir = SteamFix.NewBackupFolder(targetId);
        var noBom = new UTF8Encoding(false);
        File.WriteAllText(Path.Combine(dir, "original-developer-metadata-PRIVATE.json"), initiallyRead.Raw, noBom);

        string? originalBackup = string.IsNullOrWhiteSpace(backupJsonPath) ? null : ReadSmallFile(backupJsonPath);
        string? project = string.IsNullOrWhiteSpace(vrgprojPath) ? null : ReadSmallFile(vrgprojPath);
        string? local = string.IsNullOrWhiteSpace(modhubPath) ? null : ReadSmallFile(modhubPath);
        ulong? comparisonId = null;
        if (!string.IsNullOrWhiteSpace(comparisonInput)) {
            if (!SteamFix.TryGetId(comparisonInput, out ulong id))
                throw new ArgumentException("Invalid reference Workshop item ID.");
            comparisonId = id;
        }

        // The pure session reads the target again, builds an owner-checked
        // candidate, then RE-QUERIES both target and reference, if provided.
        // No write method exists anywhere in this workflow.
        var session = RecoveryReadOnlySession.Prepare(targetId, owner, id => {
            RemoteInfo live = SteamFix.ReadRemote(id);
            return new RecoveryRemoteSnapshot(
                id, RecoveryReadOnlySession.ExpectedAppId, live.Owner,
                QuerySucceeded: true, IsCached: false, DeveloperMetadata: live.Raw);
        }, originalBackup, project, local, comparisonId);

        if (session.OriginalRaw != initiallyRead.Raw)
            throw new InvalidOperationException(
                "Original metadata changed since first diagnostic query. Preview rejected.");

        if (originalBackup is not null)
            File.WriteAllText(Path.Combine(dir, "source-backup-PRIVATE.json"), originalBackup, noBom);
        if (project is not null)
            File.WriteAllText(Path.Combine(dir, "original-project-PRIVATE.json"), project, noBom);
        if (local is not null)
            File.WriteAllText(Path.Combine(dir, "original-modhub-metadata-PRIVATE.json"), local, noBom);
        if (session.ReferenceRaw is not null)
            File.WriteAllText(Path.Combine(dir, "working-reference-PRIVATE.json"), session.ReferenceRaw, noBom);
        File.WriteAllText(Path.Combine(dir, "candidate-preview-PRIVATE.json"),
            session.Plan.CandidateJson, noBom);

        string source = session.Plan.Source switch {
            RecoverySource.OriginalBackup => "Verified original same-item backup",
            RecoverySource.WorkingItem => "Original project and owned comparison schema",
            RecoverySource.GenericReference => "Synthetic reference, unverified, preview-only",
            _ => throw new InvalidOperationException("Unknown recovery source")
        };
        string mode = session.Plan.Source == RecoverySource.GenericReference
            ? "EXPERIMENTAL PREVIEW ONLY — no upload permitted"
            : "REVIEWABLE CANDIDATE — owner approval and a fresh Steam check required";

        var report = new {
            status = "PREVIEW COMPLETE — NO STEAM CHANGES",
            workshopItem = targetId,
            classification = session.Condition.ToString(),
            originalBytes = Encoding.UTF8.GetByteCount(session.OriginalRaw),
            originalSha256 = session.OriginalSha256,
            candidateBytes = session.Plan.CandidateBytes,
            candidateSha256 = session.CandidateSha256,
            schemaTemplateSha256 = session.ReferenceSha256,
            source, mode,
            warning = session.Plan.Explanation,
            steamChanges = false
        };
        File.WriteAllText(Path.Combine(dir, "report-SAFE-TO-SHARE.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), noBom);

        string result = "READ-ONLY RECOVERY PREVIEW COMPLETE\r\n"
            + "Original Workshop item: " + targetId + "\r\n"
            + "Detected state: " + session.Condition + "\r\n"
            + "Recovery source: " + source + "\r\n"
            + "Candidate size: " + session.Plan.CandidateBytes + " UTF-8 bytes\r\n"
            + "Candidate SHA256: " + session.CandidateSha256 + "\r\n"
            + "Status: " + mode + "\r\n"
            + "PREVIEW IS READ-ONLY. A separate, confirmed RESTORE is available for verified sources.\r\n"
            + "Private backup directory: " + dir + "\r\n"
            + session.Plan.Explanation;
        return new(result, dir, session,
            project is null ? null : SteamFix.Hash(project),
            local is null ? null : SteamFix.Hash(local));
    }

    private static string ReadSmallFile(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length > 128 * 1024)
            throw new ArgumentException("Source file missing or over 128 KiB: " + path);
        return File.ReadAllText(file.FullName, new UTF8Encoding(false, true));
    }
}

internal sealed class RecoveryPreviewForm : Form
{
    private readonly TextBox _backupFile = new() { Dock = DockStyle.Fill, PlaceholderText = "Optional: full developer-metadata backup for this SAME item" };
    private readonly TextBox _vrgproj = new() { Dock = DockStyle.Fill, PlaceholderText = "Original .vrgproj project file (required without backup)" };
    private readonly TextBox _local = new() { Dock = DockStyle.Fill, PlaceholderText = "Optional: original ModProject.modhub-metadata" };
    private readonly TextBox _comparison = new() { Dock = DockStyle.Fill, PlaceholderText = "Optional: your OTHER working Workshop item ID" };
    private readonly TextBox _result = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both };
    private readonly Button _preview = new() { Dock = DockStyle.Fill, Text = "BUILD READ-ONLY RECOVERY PREVIEW", Height = 44 };
    private readonly TextBox _confirmation = new() {
        Dock = DockStyle.Fill, PlaceholderText = "To apply: RESTORE <original Workshop ID>"
    };
    private readonly Button _apply = new() {
        Dock = DockStyle.Fill, Text = "RESTORE MY WORKSHOP METADATA (CONFIRM FIRST)",
        Enabled = false, Height = 42
    };
    private RecoveryPreviewResult? _ready;
    private readonly ulong _id;

    internal RecoveryPreviewForm(ulong workshopId)
    {
        _id = workshopId;
        Text = "Safe Recovery Preview — Workshop item " + workshopId;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new System.Drawing.Size(730, 670);
        ClientSize = new System.Drawing.Size(830, 745);
        Font = new System.Drawing.Font("Segoe UI", 10);
        var layout = new TableLayoutPanel {
            Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 10
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        for (int n = 0; n < 4; n++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        Controls.Add(layout);
        layout.Controls.Add(new Label {
            Text = "This is a safe read-only proposal. Choose an original full backup OR original project files. " +
                   "A reference mod is optional; without one the proposal is experimental and NOT uploadable.",
            Dock = DockStyle.Fill, AutoSize = false
        }, 0, 0);

        AddFileField(layout, 1, "Exact original developer metadata backup (highest confidence)", _backupFile, "JSON files|*.json|All files|*.*");
        AddFileField(layout, 2, "Original .vrgproj (required without exact backup)", _vrgproj, "VRage projects|*.vrgproj|All files|*.*");
        AddFileField(layout, 3, "Original ModHUB metadata (optional)", _local, "ModHUB metadata|*.modhub-metadata|All files|*.*");
        var comparisonPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        comparisonPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
        comparisonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        comparisonPanel.Controls.Add(new Label { Text = "Working reference Workshop item ID (optional)", Dock = DockStyle.Fill }, 0, 0);
        comparisonPanel.Controls.Add(_comparison, 0, 1);
        layout.Controls.Add(comparisonPanel, 0, 4);
        layout.Controls.Add(_preview, 0, 5);
        layout.Controls.Add(_result, 0, 6);
        layout.Controls.Add(_confirmation, 0, 7);
        layout.Controls.Add(_apply, 0, 8);
        layout.Controls.Add(new Label {
            Text = "Writes affect INTERNAL developer metadata only. Public description, title and content are not set by this tool.",
            Dock = DockStyle.Fill, ForeColor = System.Drawing.Color.FromArgb(96, 98, 106)
        }, 0, 9);

        // Input changes invalidate the entire prior live Steam preview.
        foreach (var source in new[] { _backupFile, _vrgproj, _local, _comparison })
            source.TextChanged += (_, _) => InvalidatePreview();
        _confirmation.TextChanged += (_, _) => UpdateApplyAvailability();
        _preview.Click += async (_, _) => await PreviewAsync();
        _apply.Click += async (_, _) => await ApplyAsync();
    }

    private static void AddFileField(TableLayoutPanel layout, int row, string name, TextBox textBox, string filter)
    {
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(new Label { Dock = DockStyle.Fill, Text = name }, 0, 0);
        var chooser = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        chooser.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        chooser.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        var browse = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        browse.Click += (_, _) => {
            using var dialog = new OpenFileDialog { Title = name, Filter = filter, CheckFileExists = true };
            if (dialog.ShowDialog() == DialogResult.OK) textBox.Text = dialog.FileName;
        };
        chooser.Controls.Add(textBox, 0, 0);
        chooser.Controls.Add(browse, 1, 0);
        outer.Controls.Add(chooser, 0, 1);
        layout.Controls.Add(outer, 0, row);
    }

    private void InvalidatePreview()
    {
        _ready = null;
        _confirmation.Clear();
        _apply.Enabled = false;
    }

    private void UpdateApplyAvailability()
    {
        _apply.Enabled = _ready is { Session.Plan.EligibleForApply: true } &&
            (_ready.Session.Plan.Source is RecoverySource.OriginalBackup or RecoverySource.WorkingItem) &&
            _confirmation.Text == "RESTORE " + _id;
    }

    private async Task ApplyAsync()
    {
        if (!_apply.Enabled || _ready is not { } selected) return;
        if (MessageBox.Show(this,
            "This WILL update ONLY developer metadata on your owned Workshop item " + _id +
            ".\n\nSource: " + selected.Session.Plan.Source +
            "\nCandidate size: " + selected.Session.Plan.CandidateBytes + " bytes" +
            "\n\nThe public Workshop title, description, files and preview image are not updated." +
            "\n\nThe original remote data and all sources are saved locally. " +
            "Steam ownership and unchanged original bytes will be rechecked immediately before update." +
            "\n\nDo you authorize this ONE metadata-only Steam update?",
            "Confirm Steam developer metadata RESTORE",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        _preview.Enabled = false;
        _apply.Enabled = false;
        string typedConfirmation = _confirmation.Text; // Snapshot on UI thread, not inside Task.Run.
        _confirmation.Enabled = false;
        _result.Text = "Verifying source files, owner and Steam metadata; submitting one update...";
        try {
            var applied = await Task.Run(() =>
                RecoveryApplyService.Apply(selected, typedConfirmation));
            _result.Text = applied.SafeSummary +
                "\r\nSafe result report: " + applied.SafeReportPath;
            MessageBox.Show(this,
                "Steam returned the exact recovered developer metadata. " +
                "Please restart ModHUB and verify the mod in SE2.",
                "Restore readback verified", MessageBoxButtons.OK, MessageBoxIcon.Information);
        } catch (Exception ex) {
            _result.Text = "RESTORE NOT CONFIRMED — do not retry blindly.\r\n" +
                ex.Message + "\r\nPrivate backup: " + selected.BackupFolder;
            MessageBox.Show(this,
                "Restore could not be confirmed. Do not repeat a Steam write " +
                "until the item has been inspected again.\n\n" + ex.Message,
                "Restore stopped", MessageBoxButtons.OK, MessageBoxIcon.Error);
        } finally {
            _ready = null; // Require a fresh preview after ANY attempted update.
            _confirmation.Enabled = true;
            _confirmation.Clear();
            _preview.Enabled = true;
            _apply.Enabled = false;
        }
    }

    private async Task PreviewAsync()
    {
        if (_preview.Enabled == false) return;
        InvalidatePreview();
        string originalBackup = _backupFile.Text.Trim();
        string project = _vrgproj.Text.Trim();
        string local = _local.Text.Trim();
        string working = _comparison.Text.Trim();
        _preview.Enabled = false;
        _result.Text = "Checking ownership and live Steam state; no changes will be made...";
        try {
            RecoveryPreviewResult info = await Task.Run(() =>
                RecoveryPreviewService.Prepare(_id, originalBackup, project, local, working));
            _ready = info;
            _result.Text = info.SafeSummary +
                (info.Session.Plan.EligibleForApply &&
                 (info.Session.Plan.Source is RecoverySource.OriginalBackup or RecoverySource.WorkingItem)
                    ? "\r\n\r\nEligible source: enter RESTORE " + _id +
                      " below to enable the one-time metadata-only restore."
                    : "\r\n\r\nUnverified source. Steam upload stays BLOCKED.");
            UpdateApplyAvailability();
        } catch (Exception ex) {
            _result.Text = "PREVIEW STOPPED. NO STEAM CHANGES.\r\n" + ex.Message;
        } finally { _preview.Enabled = true; }
    }
}
