#!/usr/bin/env python3
"""Static source-only WPF preview gate: no Steam API interaction and no release claim."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
wpf = root / "src/SE2WorkshopUGCFix.Wpf"
legacy = root / "legacy-gui-v0.3.0/SE2WorkshopUGCFix"
docs = root / "docs/WPF-PREVIEW.md"
contents = {
    p.name: p.read_text(encoding="utf-8")
    for p in list(wpf.glob("*.cs")) + list(wpf.glob("*.xaml"))
}
for p in wpf.glob("*.xaml"):
    ET.parse(p)
assert len(list(wpf.glob("*.xaml"))) == 5
project = (wpf / "SE2WorkshopUGCFix.Wpf.csproj").read_text(encoding="utf-8")
legacy_project = (legacy / "SE2WorkshopUGCFix.csproj").read_text(encoding="utf-8")
friend = (legacy / "Properties/AssemblyInfo.cs").read_text(encoding="utf-8")
assert "<UseWPF>true</UseWPF>" in project
assert "<ImplicitUsings>disable</ImplicitUsings>" in project, (
    "WinForms implicit global usings cause ambiguous WPF type names")
assert "<EnableWindowsTargeting>true</EnableWindowsTargeting>" in project
assert "ProjectReference" in project and "EnableFixtureLab=false" in project
assert "InternalsVisibleTo" in friend and "SE2WorkshopUGCFix.Wpf" in friend
assert '<Compile Remove="Disposable*.cs;TestItem*.cs" />' in legacy_project
assert "Condition=\"'$(EnableFixtureLab)' == 'true'\"" in legacy_project
assert "SteamFix.Scan(id)" in contents["MainWindow.xaml.cs"]
assert "SteamReadProbe.Run(id)" in contents["MainWindow.xaml.cs"]
assert "SafeReportGuard.ReadValidated(_safeReportPath)" in contents["MainWindow.xaml.cs"]
assert "Clipboard.SetText(safeReport)" in contents["MainWindow.xaml.cs"]
assert "Clipboard.SetText(File.ReadAllText(" not in contents["MainWindow.xaml.cs"]
assert "DirectoryNotFoundException(" in contents["MainWindow.xaml.cs"]
assert "Safe report not copied:" in contents["MainWindow.xaml.cs"]
guard = contents["SafeReportGuard.cs"]
assert 'report-SAFE-TO-SHARE.json' in guard
assert "HashSet<string> ScanFields" in guard
assert "HashSet<string> ProbeFields" in guard
assert "contentUpdated" in guard and "steamUpdatesSubmitted" in guard
assert "JsonValueKind.Object" in guard and "JsonValueKind.Array" in guard
assert "MaximumReportBytes" in guard
assert "Duplicate report fields are not permitted." in guard
assert "HasExpectedType(field.Name, field.Value.ValueKind)" in guard
assert "Only report-SAFE-TO-SHARE.json may be copied." in guard
assert "Clipboard guard allowed private original metadata." in contents["App.xaml.cs"]
assert "Clipboard guard allowed a private backup filename." in contents["App.xaml.cs"]

assert "SteamFix.Apply(candidate)" in contents["MainWindow.xaml.cs"]
assert "RecoveryPreviewService.Prepare(" in contents["RecoveryWindow.xaml.cs"]
assert "RecoveryApplyService.Apply(selected, typed)" in contents["RecoveryWindow.xaml.cs"]
assert "DescriptionPreflight.Calculate(" in contents["PreflightWindow.xaml.cs"]
assert 'TextChanged="DescriptionChanged"' in contents["PreflightWindow.xaml"]
assert "CopyButton.IsEnabled = false;" in contents["PreflightWindow.xaml.cs"]
assert "Description changed. Run Estimate size again." in contents["PreflightWindow.xaml.cs"]
assert "Estimate failed. No Steam write was performed." in contents["PreflightWindow.xaml.cs"]
assert "Changing recovery sources" not in contents["RecoveryWindow.xaml.cs"], "No stale rollback ambiguity"
assert "Sources changed. Build a new read-only preview" in contents["RecoveryWindow.xaml.cs"]
assert "offlineReviewOnly = false" in contents["RecoveryWindow.xaml.cs"]
assert "if (_busy || _offlineReviewOnly) return;" in contents["RecoveryWindow.xaml.cs"]
assert "PreviewButton.IsEnabled = false;" in contents["RecoveryWindow.xaml.cs"]
assert "RestoreButton.IsEnabled = !_offlineReviewOnly && !_busy" in contents["RecoveryWindow.xaml.cs"]
assert 'e.Args[0] == "--review-recovery-ui"' in contents["App.xaml.cs"]
assert "new RecoveryWindow(12345678UL, offlineReviewOnly: true)" in contents["App.xaml.cs"]
assert "Offline description estimator failed." in contents["App.xaml.cs"]
assert "An outdated description estimate was still copyable." in contents["App.xaml.cs"]
assert "Offline recovery review exposed actionable controls." in contents["App.xaml.cs"]
assert "Offline recovery review mislabeled as a live recovery session." in contents["App.xaml.cs"]
for name in ("BackupBrowseButton", "ProjectBrowseButton", "ModhubBrowseButton"):
    assert f'Name="{name}"' in contents["RecoveryWindow.xaml"], name
    assert f"{name}.IsEnabled = false;" in contents["RecoveryWindow.xaml.cs"], name
    assert f"{name}.IsEnabled = !_offlineReviewOnly && !value;" in contents["RecoveryWindow.xaml.cs"], name
    assert f"review.{name}.IsEnabled" in contents["App.xaml.cs"], name
assert 'Name="RecoveryModeEyebrow"' in contents["RecoveryWindow.xaml"]
assert 'OFFLINE VISUAL REVIEW · NO STEAM ACCESS' in contents["RecoveryWindow.xaml.cs"]

assert "Changing recovery sources failed to invalidate preview." in contents["App.xaml.cs"]
package_script = (root / "scripts/package-wpf-preview-docker.sh").read_text(encoding="utf-8")
safe_report_project = root / "tests/SafeReportGuardTests/SafeReportGuardTests.csproj"
safe_report_tests = root / "tests/SafeReportGuardTests/Program.cs"
assert safe_report_project.is_file() and safe_report_tests.is_file()
assert "../../src/SE2WorkshopUGCFix.Wpf/SafeReportGuard.cs" in safe_report_project.read_text(encoding="utf-8")
test_source = safe_report_tests.read_text(encoding="utf-8")
for case in ("duplicate owner field", "duplicate report type",
             "private metadata payload", "scan classification type changed",
             "private backup filename", "oversized report file",
             "missing report file"):
    assert case in test_source, case
assert "dotnet run --project tests/SafeReportGuardTests/SafeReportGuardTests.csproj" in package_script
assert 'git diff --quiet' in package_script
assert 'SHA256-MANIFEST.txt' in package_script
assert 'SOURCE_COMMIT={commit}' in package_script
assert 'UNSIGNED_TEST_ARTIFACT=true' in package_script
assert "0.6.0-preview.8" in project and "0.6.0-preview.8" in package_script
assert "RUN-WPF-REVIEW-RECOVERY.cmd" in package_script
assert "--review-recovery-ui" in package_script
assert "0.6.0-preview.8" in project and "0.6.0-preview.8" in package_script

assert "RecoverySource.OriginalBackup or RecoverySource.WorkingItem" in contents["RecoveryWindow.xaml.cs"]
assert "RESTORE " in contents["RecoveryWindow.xaml.cs"]
assert "MessageBoxResult.No" in contents["RecoveryWindow.xaml.cs"]
assert "ResetScan();" in contents["MainWindow.xaml.cs"]
assert "ToggleLog_Click" in contents["MainWindow.xaml.cs"]
assert "StatusAccent.Background" in contents["MainWindow.xaml.cs"]
assert "OverviewScroller.ScrollableHeight > 1.0" in contents["App.xaml.cs"]
assert "ActivityPanel.Visibility != Visibility.Collapsed" in contents["App.xaml.cs"]
main = ET.parse(wpf / "MainWindow.xaml").getroot()
theme = ET.parse(wpf / "UiTheme.xaml").getroot()
ns = {"w": "http://schemas.microsoft.com/winfx/2006/xaml/presentation"}
window_width = float(main.attrib["Width"])
window_height = float(main.attrib["Height"])
assert window_width <= 1080 and window_height <= 780, "Overview grew instead of becoming compact"
sidebar = main.find(".//w:Grid/w:Grid.ColumnDefinitions/w:ColumnDefinition", ns)
assert sidebar is not None and float(sidebar.attrib["Width"]) <= 190
assert len(main.findall(".//w:ScrollViewer", ns)) == 1, "One overview-only overflow container expected"
scroller = main.find(".//w:ScrollViewer", ns)
assert scroller is not None and scroller.attrib.get("VerticalScrollBarVisibility") == "Auto"
for name in ("OverviewScroller", "ActivityPanel", "StatusAccent", "ActivityLog"):
    assert name in contents["MainWindow.xaml"], name
assert 'Visibility="Collapsed"' in contents["MainWindow.xaml"], "Diagnostics should be collapsed"
assert "SlimScrollThumbStyle" in contents["UiTheme.xaml"]
assert 'Name="WorkshopInput"' in contents["MainWindow.xaml"]
assert 'WorkshopInputNativeStyle' in contents["MainWindow.xaml"]
assert 'WorkshopInputNativeStyle' in contents["UiTheme.xaml"]
native_start = contents["UiTheme.xaml"].split('x:Key="WorkshopInputNativeStyle"', 1)[1]
native_style = native_start.split("</Style>", 1)[0]
assert "<Setter Property=\"Template\"" not in native_style, "Native input must keep WPF text host"
assert 'Property="SelectionBrush"' in native_style
assert 'Property="VerticalContentAlignment" Value="Center"' in native_style
assert 'Property="TextWrapping" Value="NoWrap"' in native_style
assert 'Property="SpellCheck.IsEnabled" Value="False"' in native_style
assert 'Property="IsKeyboardFocusWithin"' in contents["MainWindow.xaml"]
assert 'Workshop URL text entry/selection failed.' in contents["App.xaml.cs"]

assert "PART_Track" in contents["UiTheme.xaml"]
assert "ScrollBar.PageUpCommand" in contents["UiTheme.xaml"]
assert "ScrollBar.PageDownCommand" in contents["UiTheme.xaml"]
assert len(main.findall(".//w:UniformGrid", ns)) == 1, "Use a single compact summary strip"

assert "new RecoveryWindow(12345678UL)" in contents["App.xaml.cs"]
assert "--smoke-ui" in contents["App.xaml.cs"]
assert "last-wpf-smoke.txt" in contents["App.xaml.cs"]
assert "SteamAPI.Init(" not in contents["App.xaml.cs"]
for name, source in contents.items():
    if not name.endswith(".cs"):
        continue
    for forbidden in ("SteamUGC.SetItemMetadata(", "SteamUGC.SubmitItemUpdate(",
                      "SteamUGC.DeleteItem(", "SteamUGC.CreateItem("):
        assert forbidden not in source, (name, forbidden)
assert docs.exists()
print("PASS: WPF XAML XML files parse and WPF safety/source checks pass.")
print("NOTE: This static check is NOT a Windows build, GUI initialization, or Steam acceptance test.")
