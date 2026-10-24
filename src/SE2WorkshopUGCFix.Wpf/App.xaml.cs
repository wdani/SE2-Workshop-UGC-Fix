using System;
using System.Windows;

namespace SE2WorkshopUGCFix.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 1 && e.Args[0] == "--smoke-ui")
        {
            // Closing an unshown window must not terminate the app before assertions finish.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                // All three views initialize offline: no SteamAPI.Init, no Steam writes.
                var main = new MainWindow();
                var preflight = new PreflightWindow(null, null);
                var recovery = new RecoveryWindow(12345678UL);
                if (main.RepairButton.IsEnabled || main.RecoveryButton.IsEnabled ||
                    recovery.RestoreButton.IsEnabled)
                    throw new InvalidOperationException("Unsafe WPF initial action state.");

                // Offline functional smoke: estimate, invalidate on edit, never use Steam.
                preflight.DescriptionInput.Text = "Short demo description with UTF-8: äöü";
                preflight.EstimateButton.RaiseEvent(
                    new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                if (!preflight.CopyButton.IsEnabled ||
                    !preflight.EstimateStatus.Text.Contains("No Steam write"))
                    throw new InvalidOperationException(
                        "Offline description estimator failed.");
                preflight.DescriptionInput.Text += " changed";
                if (preflight.CopyButton.IsEnabled)
                    throw new InvalidOperationException(
                        "An outdated description estimate was still copyable.");

                // The review window is a strictly offline UI gallery. No
                // Steam query or recovery preview is possible via its controls.
                var review = new RecoveryWindow(12345678UL, offlineReviewOnly: true);
                if (review.PreviewButton.IsEnabled || review.RestoreButton.IsEnabled ||
                    review.BackupInput.IsEnabled || review.ConfirmInput.IsEnabled)
                    throw new InvalidOperationException(
                        "Offline recovery review exposed actionable controls.");
                recovery.BackupInput.Text = "changed backup path";
                if (recovery.RestoreButton.IsEnabled ||
                    !recovery.PreviewResult.Text.Contains("Sources changed"))
                    throw new InvalidOperationException(
                        "Changing recovery sources failed to invalidate preview.");
                review.Close();

                // Real WPF measure/arrange on Windows; the overview must fit
                // the default window without a vertical scrollbar.
                main.ShowInTaskbar = false;
                main.Opacity = 0;
                main.Show();
                main.UpdateLayout();
                if (main.OverviewScroller.ScrollableHeight > 1.0)
                    throw new InvalidOperationException(
                        "Compact overview overflowed the normal window height. " +
                        "ScrollableHeight=" + main.OverviewScroller.ScrollableHeight);
                if (main.ActivityPanel.Visibility != Visibility.Collapsed)
                    throw new InvalidOperationException(
                        "Diagnostics must start collapsed.");

                // Exercise the actual native input with a full Workshop URL and
                // ensure reading/editing never unlocks Steam-write controls.
                const string exampleUrl =
                    "https://steamcommunity.com/sharedfiles/filedetails/?id=3824260687";
                main.WorkshopInput.Text = exampleUrl;
                main.WorkshopInput.SelectAll();
                main.WorkshopInput.UpdateLayout();
                if (main.WorkshopInput.Text != exampleUrl ||
                    main.WorkshopInput.SelectionLength != exampleUrl.Length)
                    throw new InvalidOperationException(
                        "Workshop URL text entry/selection failed.");
                if (main.RepairButton.IsEnabled || main.RecoveryButton.IsEnabled)
                    throw new InvalidOperationException(
                        "Editing a Workshop ID unlocked a write action.");
                main.WorkshopInput.Clear();
                main.Close();
                preflight.Close();
                recovery.Close();
                Shutdown(0);
            }
            catch (Exception ex)
            {
                try
                {
                    var folder = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "SE2WorkshopUGCFix", "Diagnostics");
                    System.IO.Directory.CreateDirectory(folder);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"last-wpf-smoke.txt"),
                        DateTimeOffset.Now.ToString("O") + Environment.NewLine + ex);
                }
                catch { /* Exit must still fail if report cannot be persisted. */ }
                Shutdown(1);
            }
            return;
        }
        // Locked visual check of the Recovery dialog without Steam or a damaged
        // Workshop item; no live preview, submit, ownership lookup or writes.
        if (e.Args.Length == 1 && e.Args[0] == "--review-recovery-ui")
        {
            var review = new RecoveryWindow(12345678UL, offlineReviewOnly: true);
            MainWindow = review;
            review.Show();
            return;
        }
        // A live read or change requires the original Steam guards and user action.
        Environment.SetEnvironmentVariable("SteamAppId", "1133870");
        Environment.SetEnvironmentVariable("SteamGameId", "1133870");
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

}
