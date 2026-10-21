#!/usr/bin/env python3
"""Offline source safety gate. Does not compile C#, access Steam, or claim live validation."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[1]
gui = (root / "legacy-gui-v0.3.0/SE2WorkshopUGCFix/Program.cs").read_text(encoding="utf-8")
preview = (root / "legacy-gui-v0.3.0/SE2WorkshopUGCFix/RecoveryPreviewForm.cs").read_text(encoding="utf-8")
core = (root / "src/SE2WorkshopUGCFix.Recovery/UniversalRecovery.cs").read_text(encoding="utf-8")
session = (root / "src/SE2WorkshopUGCFix.Recovery/RecoveryReadOnlySession.cs").read_text(encoding="utf-8")
project = (root / "legacy-gui-v0.3.0/SE2WorkshopUGCFix/SE2WorkshopUGCFix.csproj").read_text(encoding="utf-8")
legacy = (root / "legacy-gui-v0.3.0/SE2WorkshopUGCFix/MetadataPatch.cs").read_text(encoding="utf-8")

checks = [
    ("Steam transport requires successful GetQueryUGCMetadata", 'if (!SteamUGC.GetQueryUGCMetadata(' in gui),
    ("Steam read distinguishes empty successful metadata", 'return new RemoteInfo(raw ?? "", details.m_ulSteamIDOwner);' in gui),
    ("Steam API still rejects wrong app", 'SteamUtils.GetAppID().m_AppId != AppId' in gui),
    ("Existing repair requires preview", 'if (!preview.Eligible || preview.CandidateRaw is null' in gui),
    ("Existing repair checks original state again", 'RemoteInfo now = ReadRemote(id)' in gui),
    ("Existing repair verifies exact readback", 'Hash(after.Raw) != preview.CandidateHash' in gui),
    ("UI recovery option disabled until scan", 'new() { Text = "RECOVERY PREVIEW"' in gui and 'Enabled = false' in gui),
    ("UI recovery path is explicitly preview only", 'RecoveryPreviewForm(_current.ItemId)' in gui),
    ("GUI compiles same universal recovery core", 'src/SE2WorkshopUGCFix.Recovery/UniversalRecovery.cs' in project),
    ("Recovery preview never uses Steam write methods", not any(s in preview for s in
        ("SteamUGC.StartItemUpdate", "SteamUGC.SetItemMetadata",
         "SteamUGC.SubmitItemUpdate", "SteamUGC.DeleteItem"))),
    ("Recovery core never uses Steam write methods", "SteamUGC." not in core),
    ("Preview saves source and candidate privately", 'candidate-preview-PRIVATE.json' in preview and
     'original-developer-metadata-PRIVATE.json' in preview),
    ("Preview rechecks original state",
     'session.OriginalRaw != initiallyRead.Raw' in preview and
     'RecoveryReadOnlySession.Prepare(' in preview and
     'after != original' in session),
    ("Preview rechecks unchanged working reference",
     'newReference != referenceRaw' in session),
    ("Query errors are never treated as missing metadata",
     'if (!data.QuerySucceeded)' in session and 'if (data.IsCached)' in session and
     'return data.DeveloperMetadata ??' in session),
    ("Read-only session never calls Steam API",
     'SteamUGC.' not in session and 'SteamAPI.' not in session),

    ("Generic fallback stays explicitly non-writable", 'return new(false, RecoverySource.GenericReference' in core),
    ("Original oversized one-field repair still present", 'JsonNode.DeepEquals(expected, edited)' in legacy),
    ("Template refuses unreviewed keys", 'CheckKnownTemplateKeys(template);' in core and
     'Unreviewed metadata key:' in core),
    ("Original identity is enforced", 'ReadOriginalProject(originalVrgproj, itemId)' in core),
    ("Owner is checked in recovery preview and fresh query session",
     'initiallyRead.Owner != owner' in preview and
     'if (data.OwnerId == 0 || data.OwnerId != expectedOwner)' in session and
     'SteamUser.GetSteamID().m_SteamID' in preview),
    ("UI footer grouped instead of four cramped buttons",
     "CreateActionGroup(\"BACKUPS / REPORTS\"" in gui and
     "CreateActionGroup(\"TOOLS / RECOVERY\"" in gui and
     "RowStyle(SizeType.Absolute, 148)" in gui),
    ("Healthy diagnostics show success instead of no-fix warning",
     'case MetadataCondition.Healthy:' in gui and
     'HEALTHY — Metadata is valid. No repair necessary.' in gui),
    ("Recovery preview requires actual owner before enabling",
     '_current is { OwnerMatches: true }' in gui),
    ("UI smoke checks footer caption dimensions",
     'TextRenderer.MeasureText(button.Text, button.Font)' in gui),
    ("Offline UI smoke entrypoint exists", '"--smoke-ui"' in gui and "new RecoveryPreviewForm(12345678UL)" in gui),
    ("UI smoke does not initialize Steam", 
     "SteamAPI.Init()" not in gui[gui.index('if (args.Length == 1 && args[0] == "--smoke-ui")'):gui.index('// AppID is set before SteamAPI.Init')]),

]
bad = 0
for label, ok in checks:
    print(f'{"PASS" if ok else "FAIL"}: {label}')
    if not ok:
        bad += 1
if bad:
    raise SystemExit(f'FAILED {bad} source safety checks')
print(f'PASS: {len(checks)} offline source gates. No C# compilation, Steam access or uploads occurred.')
