using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using SE2WorkshopUGCFix;

namespace SE2WorkshopUGCFix.Wpf;

public partial class RecoveryWindow : Window
{
    private readonly ulong _itemId;
    private readonly bool _offlineReviewOnly;
    private RecoveryPreviewResult? _ready;
    private bool _busy;

    public RecoveryWindow(ulong workshopId, bool offlineReviewOnly = false)
    {
        _itemId = workshopId;
        _offlineReviewOnly = offlineReviewOnly;
        InitializeComponent();
        WindowAppearance.ApplyDarkTitleBar(this);
        RecoveryHeading.Text = "Recover Workshop item " + _itemId;
        ConfirmHint.Text = "ENTER EXACTLY: RESTORE " + _itemId;
        if (_offlineReviewOnly)
        {
            Title = "Recovery assistant · offline UI review";
            RecoveryModeEyebrow.Text = "OFFLINE VISUAL REVIEW · NO STEAM ACCESS";
            RecoveryModeEyebrow.Foreground =
                (System.Windows.Media.Brush)FindResource("SuccessBrush");
            RecoveryHeading.Text = "Recovery assistant · UI review";
            ConfirmHint.Text = "LOCKED · preview-only demonstration";
            PreviewResult.Text = "OFFLINE UI REVIEW. No Steam connection, no owner verification, no recovery preview and no write authorization.";
            PreviewButton.IsEnabled = false;
            RestoreButton.IsEnabled = false;
            BackupBrowseButton.IsEnabled = false;
            ProjectBrowseButton.IsEnabled = false;
            ModhubBrowseButton.IsEnabled = false;
            BackupInput.IsEnabled = false;
            ProjectInput.IsEnabled = false;
            ModhubInput.IsEnabled = false;
            ReferenceInput.IsEnabled = false;
            ConfirmInput.IsEnabled = false;
        }
    }

    private void InvalidatePreview()
    {
        _ready = null;
        ConfirmInput.Clear();
        RestoreButton.IsEnabled = false;
    }

    private void SourceChanged(object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        InvalidatePreview();
        if (PreviewResult is not null)
            PreviewResult.Text = "Sources changed. Build a new read-only preview before any restore can be considered.";
    }

    private void ConfirmChanged(object sender,
        System.Windows.Controls.TextChangedEventArgs e) => UpdateRestore();

    private void UpdateRestore()
    {
        RestoreButton.IsEnabled = !_offlineReviewOnly && !_busy &&
            _ready is { Session.Plan.EligibleForApply: true } &&
            (_ready.Session.Plan.Source is RecoverySource.OriginalBackup or RecoverySource.WorkingItem) &&
            ConfirmInput.Text == "RESTORE " + _itemId;
    }

    private void SetBusy(bool value)
    {
        _busy = value;
        PreviewButton.IsEnabled = !_offlineReviewOnly && !value;
        BackupBrowseButton.IsEnabled = !_offlineReviewOnly && !value;
        ProjectBrowseButton.IsEnabled = !_offlineReviewOnly && !value;
        ModhubBrowseButton.IsEnabled = !_offlineReviewOnly && !value;
        BackupInput.IsEnabled = !_offlineReviewOnly && !value;
        ProjectInput.IsEnabled = !_offlineReviewOnly && !value;
        ModhubInput.IsEnabled = !_offlineReviewOnly && !value;
        ReferenceInput.IsEnabled = !_offlineReviewOnly && !value;
        ConfirmInput.IsEnabled = !_offlineReviewOnly && !value;
        UpdateRestore();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _offlineReviewOnly || sender is not System.Windows.Controls.Button button) return;
        var kind = button.Tag?.ToString();
        var dialog = new OpenFileDialog {
            CheckFileExists = true,
            Filter = kind switch {
                "backup" => "JSON original backups|*.json|All files|*.*",
                "project" => "VRage project|*.vrgproj|All files|*.*",
                _ => "ModHUB metadata|*.modhub-metadata|All files|*.*"
            }
        };
        if (dialog.ShowDialog(this) != true) return;
        if (kind == "backup") BackupInput.Text = dialog.FileName;
        else if (kind == "project") ProjectInput.Text = dialog.FileName;
        else ModhubInput.Text = dialog.FileName;
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _offlineReviewOnly) return;
        InvalidatePreview();
        var backup = BackupInput.Text.Trim();
        var project = ProjectInput.Text.Trim();
        var modhub = ModhubInput.Text.Trim();
        var reference = ReferenceInput.Text.Trim();
        SetBusy(true);
        PreviewResult.Text = "Checking Steam ownership, sources and candidate. No changes to Steam...";
        try
        {
            var result = await Task.Run(() =>
                RecoveryPreviewService.Prepare(_itemId, backup, project, modhub, reference));
            _ready = result;
            PreviewResult.Text = result.SafeSummary +
                (result.Session.Plan.EligibleForApply &&
                result.Session.Plan.Source is RecoverySource.OriginalBackup or RecoverySource.WorkingItem
                    ? "\n\nA verified source is available. Review the candidate and type the confirmation below."
                    : "\n\nUNVERIFIED SOURCE: Preview only. Steam write is blocked.");
        }
        catch (Exception ex)
        {
            // A failure after assigning _ready must not leave a proposal
            // eligible for submission on the next UI event.
            InvalidatePreview();
            PreviewResult.Text = "PREVIEW STOPPED. NO STEAM CHANGES.\n" + ex.Message;
        }
        finally { SetBusy(false); }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _offlineReviewOnly || !RestoreButton.IsEnabled || _ready is not { } selected) return;
        var message =
            $"This WILL update ONLY internal developer metadata for your owned Workshop item {_itemId}.\n\n" +
            $"Source: {selected.Session.Plan.Source}\nCandidate bytes: {selected.Session.Plan.CandidateBytes}\n\n" +
            "Public title, description, mod files and thumbnails are not updated. " +
            "Steam ownership, unchanged remote bytes and local backups are rechecked.\n\n" +
            "Authorize ONE Steam metadata-only update?";
        if (MessageBox.Show(this, message, "Final Steam restore approval", MessageBoxButton.YesNo,
            MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;
        string typed = ConfirmInput.Text;
        SetBusy(true);
        PreviewResult.Text = "Verifying and submitting exactly one approved Steam metadata update...";
        try
        {
            var result = await Task.Run(() => RecoveryApplyService.Apply(selected, typed));
            PreviewResult.Text = result.SafeSummary + "\n\nSafe report: " + result.SafeReportPath;
            MessageBox.Show(this, "Exact Steam metadata readback verified. Restart ModHUB and test the mod in SE2.",
                "Restore confirmed", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            PreviewResult.Text = "RESTORE NOT CONFIRMED — DO NOT RETRY BLINDLY.\n" +
                ex.Message + "\n\nPrivate source directory: " + selected.BackupFolder;
            MessageBox.Show(this, "Result not confirmed. Do not repeat the write before inspecting the current Workshop state.\n\n" +
                ex.Message, "Steam write stopped", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _ready = null;
            ConfirmInput.Clear();
            SetBusy(false);
        }
    }
}
