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
        // A live read or change requires the original Steam guards and user action.
        Environment.SetEnvironmentVariable("SteamAppId", "1133870");
        Environment.SetEnvironmentVariable("SteamGameId", "1133870");
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

}
