using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using SE2WorkshopUGCFix;

namespace SE2WorkshopUGCFix.Wpf;

public partial class MainWindow : Window
{
    private ScanInfo? _current;
    private string? _safeReportPath;
    private string? _backupDirectory;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        WindowAppearance.ApplyDarkTitleBar(this);
        AddLog("WPF preview loaded. No Steam requests run automatically.");
        AddLog("An update requires a matching Steam owner, verified backup and explicit approval.");
    }

    private void AddLog(string line)
    {
        ActivityLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
        ActivityLog.ScrollToEnd();
    }

    private static void Fail(string title, Exception ex) =>
        MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    private bool TryItemId(out ulong id)
    {
        if (SteamFix.TryGetId(WorkshopInput.Text, out id)) return true;
        MessageBox.Show(this, "Enter a valid Workshop ID or Steam Community item URL.",
            "Invalid Workshop item", MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
    }

    private void SetStatus(string title, string detail, Brush? color = null)
    {
        StatusCaption.Text = title;
        StatusDetail.Text = detail;
        StatusCaption.Foreground = color ?? (Brush)FindResource("TextBrush");
    }

    private void ResetScan()
    {
        _current = null;
        _backupDirectory = null;
        _safeReportPath = null;
        RepairButton.IsEnabled = false;
        RecoveryButton.IsEnabled = false;
        NavRecoveryButton.IsEnabled = false;
        BackupButton.IsEnabled = false;
        ReportButton.IsEnabled = false;
        ConditionValue.Text = "Not checked";
        OwnerValue.Text = "Unknown";
        BytesValue.Text = "— / 5,000";
        ActionValue.Text = "Inspect first";
        SummaryTitle.Text = "No item inspected";
        SummaryDetail.Text = "The result and recommended next step will appear here.";
    }

    private void WorkshopInput_TextChanged(object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        // New item or changed ID invalidates previously approved suggestions.
        ResetScan();
        SetStatus("Ready for inspection", "The item selection changed. Run a new check.");
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        CheckButton.IsEnabled = !busy;
        ProbeButton.IsEnabled = !busy;
        WorkshopInput.IsEnabled = !busy;
        RepairButton.IsEnabled = !busy && _current?.Eligible == true;
        bool canRecover = !_busy && _current?.OwnerMatches == true &&
            (_current.Diagnosis.Condition is MetadataCondition.Missing or MetadataCondition.Malformed);
        RecoveryButton.IsEnabled = canRecover;
        NavRecoveryButton.IsEnabled = canRecover;
        BackupButton.IsEnabled = !busy && _backupDirectory is not null && Directory.Exists(_backupDirectory);
        ReportButton.IsEnabled = !busy && _safeReportPath is not null && File.Exists(_safeReportPath);
        Cursor = busy ? System.Windows.Input.Cursors.Wait : System.Windows.Input.Cursors.Arrow;
    }

    private async void CheckButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !TryItemId(out ulong id)) return;
        ResetScan();
        SetBusy(true);
        SetStatus("Checking Steam metadata", "Fresh metadata query. No changes to Workshop data.");
        AddLog($"Checking item {id}...");
        try
        {
            var info = await Task.Run(() => SteamFix.Scan(id));
            _current = info;
            _backupDirectory = info.BackupDirectory;
            _safeReportPath = Path.Combine(info.BackupDirectory, "report-SAFE-TO-SHARE.json");
            ConditionValue.Text = info.Diagnosis.Condition.ToString();
            OwnerValue.Text = info.OwnerMatches ? "Verified" : "Other owner";
            BytesValue.Text = $"{info.OriginalBytes:N0} / 5,000";
            SummaryTitle.Text = $"{info.Title}  ·  {info.ItemId}";
            SummaryDetail.Text = info.Diagnosis.Explanation + Environment.NewLine + info.Reason;
            ActionValue.Text = info.Eligible ? "One-field fix" :
                info.OwnerMatches && info.Diagnosis.Condition is MetadataCondition.Missing or MetadataCondition.Malformed
                    ? "Recovery preview" : "Read-only";
            if (!info.OwnerMatches)
                SetStatus("Not your Workshop item",
                    "The signed-in Steam account is not the owner. All changes remain blocked.",
                    (Brush)FindResource("WarningBrush"));
            else
                switch (info.Diagnosis.Condition)
                {
                    case MetadataCondition.Healthy:
                        SetStatus("Metadata healthy", "No repair necessary.",
                            (Brush)FindResource("SuccessBrush"));
                        break;
                    case MetadataCondition.OversizedDescription when info.Eligible:
                        SetStatus("Repair candidate available",
                            "A strictly limited internal description repair can be reviewed.",
                            (Brush)FindResource("WarningBrush"));
                        break;
                    case MetadataCondition.Missing:
                    case MetadataCondition.Malformed:
                        SetStatus("Recovery sources required",
                            "Preview your original backup or verified sources before considering any update.",
                            (Brush)FindResource("WarningBrush"));
                        break;
                    case MetadataCondition.IdentityConflict:
                        SetStatus("Identity conflict — blocked",
                            "Unverified Workshop and project identity. No repair permitted.",
                            (Brush)FindResource("DangerBrush"));
                        break;
                    default:
                        SetStatus("No safe automated repair", info.Reason,
                            (Brush)FindResource("WarningBrush"));
                        break;
                }
            AddLog($"SHA256: {info.OriginalHash}");
            AddLog($"Original metadata backed up privately: {info.BackupDirectory}");
            AddLog($"Diagnosis: {info.Diagnosis.Condition}. {info.Reason}");
        }
        catch (Exception ex)
        {
            SetStatus("Check failed — nothing changed", ex.Message,
                (Brush)FindResource("DangerBrush"));
            AddLog("STOP: " + ex.Message);
            Fail("Steam metadata check failed", ex);
        }
        finally { SetBusy(false); }
    }

    private async void ProbeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !TryItemId(out ulong id)) return;
        ResetScan(); // A probe cannot unlock a repair.
        SetBusy(true);
        SetStatus("Reading Steam API flags", "A read-only query without Steam updates.");
        try
        {
            var info = await Task.Run(() => SteamReadProbe.Run(id));
            _safeReportPath = info.ReportFile;
            _backupDirectory = Path.GetDirectoryName(info.ReportFile);
            ConditionValue.Text = info.Assessment.Status.ToString();
            BytesValue.Text = info.Assessment.MetadataBytes is int bytes
                ? $"{bytes:N0} / 5,000" : "Unknown";
            ActionValue.Text = "Read-only";
            OwnerValue.Text = "Not verified";
            SummaryTitle.Text = "Raw Steam API probe · " + id;
            SummaryDetail.Text = info.Assessment.Explanation;
            SetStatus("Probe complete — no changes", info.Assessment.Explanation,
                (Brush)FindResource("SuccessBrush"));
            AddLog("Read-only probe: " + info.Assessment.Status);
        }
        catch (Exception ex)
        {
            SetStatus("Probe failed — no changes", ex.Message,
                (Brush)FindResource("DangerBrush"));
            AddLog("Probe stopped: " + ex.Message);
            Fail("Read-only probe failed", ex);
        }
        finally { SetBusy(false); }
    }

    private async void RepairButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _current is not { Eligible: true } candidate ||
            !SteamFix.TryGetId(WorkshopInput.Text, out ulong id) || id != candidate.ItemId)
            return;
        var msg = "This WILL update internal Steam developer metadata for YOUR Workshop item " +
            candidate.ItemId + ".\n\nOriginal bytes: " + candidate.OriginalBytes +
            "\nProposed bytes: " + candidate.CandidateBytes +
            "\n\nOnly FullMetadata.BaseMetadata.Description will be cleared." +
            "\nPublic Workshop descriptions, mod files, previews and saves are not updated." +
            "\n\nA private original backup must exist and identity will be freshly verified." +
            "\n\nAuthorize one metadata-only Steam submission?";
        if (MessageBox.Show(this, msg, "Confirm metadata repair", MessageBoxButton.YesNo,
            MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        SetBusy(true);
        SetStatus("Submitting guarded repair", "Do not close this window while Steam responds.");
        try
        {
            string result = await Task.Run(() => SteamFix.Apply(candidate));
            SetStatus("Repair verified", "Steam readback matched the approved candidate.",
                (Brush)FindResource("SuccessBrush"));
            AddLog(result);
            MessageBox.Show(this,
                "Steam confirmed the exact metadata repair. Restart ModHUB and test the mod in SE2." +
                "\nDo not overwrite existing saves before verification.",
                "Readback verified", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            SetStatus("Result not confirmed — STOP",
                "Do not retry without inspecting the item and private backup.",
                (Brush)FindResource("DangerBrush"));
            AddLog("STOP: " + ex.Message);
            try { File.WriteAllText(Path.Combine(candidate.BackupDirectory,"last-error.txt"),ex.ToString()); }
            catch { }
            Fail("Do not retry this Steam submission", ex);
        }
        finally
        {
            _current = null;
            SetBusy(false);
        }
    }

    private void OpenRecovery_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _current is not { OwnerMatches: true } scanned ||
            scanned.Diagnosis.Condition is not (MetadataCondition.Missing or MetadataCondition.Malformed))
            return;
        new RecoveryWindow(scanned.ItemId) { Owner = this }.ShowDialog();
        // Invalidate any snapshot when returning from a write-capable dialog.
        ResetScan();
        SetStatus("Run a fresh check", "Recovery closed. Reinspect the current Steam state.");
    }

    private void OpenPreflight_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        new PreflightWindow(_current?.OriginalRaw, _current?.ItemId) { Owner = this }.ShowDialog();
    }

    private void Overview_Click(object sender, RoutedEventArgs e) => Activate();

    private void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_backupDirectory is null || !Directory.Exists(_backupDirectory)) return;
        try { Process.Start(new ProcessStartInfo { FileName = _backupDirectory, UseShellExecute = true }); }
        catch (Exception ex) { Fail("Could not open backup folder", ex); }
    }

    private void ReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_safeReportPath is null || !File.Exists(_safeReportPath)) return;
        try
        {
            Clipboard.SetText(File.ReadAllText(_safeReportPath));
            AddLog("Safe report copied. Private original and candidate JSON were not copied.");
        }
        catch (Exception ex) { Fail("Could not copy safe report", ex); }
    }
}
