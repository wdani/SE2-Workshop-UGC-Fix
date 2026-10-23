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
assert "SteamFix.Apply(candidate)" in contents["MainWindow.xaml.cs"]
assert "RecoveryPreviewService.Prepare(" in contents["RecoveryWindow.xaml.cs"]
assert "RecoveryApplyService.Apply(selected, typed)" in contents["RecoveryWindow.xaml.cs"]
assert "DescriptionPreflight.Calculate(" in contents["PreflightWindow.xaml.cs"]
assert "RecoverySource.OriginalBackup or RecoverySource.WorkingItem" in contents["RecoveryWindow.xaml.cs"]
assert "RESTORE " in contents["RecoveryWindow.xaml.cs"]
assert "MessageBoxResult.No" in contents["RecoveryWindow.xaml.cs"]
assert "ResetScan();" in contents["MainWindow.xaml.cs"]
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
