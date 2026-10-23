#!/usr/bin/env python3
"""Offline source safety gate. Does not compile C#, access Steam, or claim live validation."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[1]
gui = (root / "legacy-gui-v0.3.0/SE2WorkshopUGCFix/Program.cs").read_text(encoding="utf-8")
preview = (root / "legacy-gui-v0.3.0/SE2WorkshopUGCFix/RecoveryPreviewForm.cs").read_text(encoding="utf-8")
apply = (root / "legacy-gui-v0.3.0/SE2WorkshopUGCFix/RecoveryApplyService.cs").read_text(encoding="utf-8")
core = (root / "src/SE2WorkshopUGCFix.Recovery/UniversalRecovery.cs").read_text(encoding="utf-8")
session = (root / "src/SE2WorkshopUGCFix.Recovery/RecoveryReadOnlySession.cs").read_text(encoding="utf-8")
preflight = (root / "src/SE2WorkshopUGCFix.Recovery/RecoveryWritePreflight.cs").read_text(encoding="utf-8")
probe = (root / "legacy-gui-v0.3.0/SE2WorkshopUGCFix/SteamReadProbe.cs").read_text(encoding="utf-8")
probe_policy = (root / "src/SE2WorkshopUGCFix.Recovery/SteamMetadataProbePolicy.cs").read_text(encoding="utf-8")
creator = (root / "legacy-gui-v0.3.0/SE2WorkshopUGCFix/TestItemCreator.cs").read_text(encoding="utf-8")
disposable = (root / "src/SE2WorkshopUGCFix.Recovery/DisposableModScenarios.cs").read_text(encoding="utf-8")
engine = (root / "src/SE2WorkshopUGCFix.Recovery/DisposableE2EEngine.cs").read_text(encoding="utf-8")
e2e_runner = (root / "legacy-gui-v0.3.0/SE2WorkshopUGCFix/DisposableE2ERunService.cs").read_text(encoding="utf-8")
e2e_dialog = (root / "legacy-gui-v0.3.0/SE2WorkshopUGCFix/DisposableE2EForm.cs").read_text(encoding="utf-8")
fixture = (root / "src/SE2WorkshopUGCFix.Recovery/TestFixturePolicy.cs").read_text(encoding="utf-8")
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
    ("Standard recovery GUI keeps APPLY locked before verified preview",
     'Text = "RESTORE MY WORKSHOP METADATA (CONFIRM FIRST)"' in preview and
     'Enabled = false, Height = 42' in preview and
     'RecoveryApplyService.Apply(selected, typedConfirmation)' in preview and
     '"RESTORE " + _id' in preview),
    ("Normal recovery uses the tested owner-only metadata-only Steam writer",
     'new OwnerOnlyMetadataTransport(' in apply and
     "SubmitMetadataOnlyIfUnchanged(" in apply and
     "SetItemMetadata(" in e2e_runner and
     'SteamUGC.SetItemTitle(' not in apply and
     'SteamUGC.SetItemDescription(' not in apply and
     'SteamUGC.SetItemContent(' not in apply),
    ("Source and candidate integrity checked before recovery APPLY",
     'RecoveryWritePreflight.Authorize(' in apply and
     'ProjectSourceSha256' in apply and
     'LocalSourceSha256' in apply and
     'source-backup-PRIVATE.json' in apply and
     'candidate-preview-PRIVATE.json' in apply),
    ("Recovery APPLY requires exact fresh Steam readback",
     'writer.ReadFresh(session.ItemId)' in apply and
     'after.Raw != approved.CandidateJson' in apply and
     'SteamFix.Hash(after.Raw) != approved.CandidateSha256' in apply),
    ("Unknown source still blocked, no blind Steam retry",
     'RecoverySource.OriginalBackup or RecoverySource.WorkingItem' in apply and
     'RecoverySource.GenericReference' not in apply and
     'NO UPDATE SENT' in e2e_runner and
     'StartItemUpdate(' not in apply),
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

    ("Future-apply preflight has NO Steam API or filesystem writes",
     "SteamUGC." not in preflight and "SteamAPI." not in preflight and
     "File.WriteAll" not in preflight and "File.Delete" not in preflight),
    ("Future-apply preflight rejects generic source",
     "preview.Plan.Source is not (RecoverySource.OriginalBackup or RecoverySource.WorkingItem)" in preflight),
    ("Future-apply requires exact typed item-specific phrase",
     '"RESTORE " + preview.ItemId' in preflight),
    ("Future-apply checks private originals/candidate and hashes",
     "Hash(storedOriginalBackup) != preview.OriginalSha256" in preflight and
     "Hash(storedCandidateBackup) != preview.CandidateSha256" in preflight),
    ("Future-apply checks fresh reference and target remotely",
     "RecoveryReadOnlySession.CheckFresh(" in preflight and
     "readFresh(referenceId)" in preflight and "readFresh(preview.ItemId)" in preflight),
    ("Generic fallback stays explicitly non-writable", 'return new(false, RecoverySource.GenericReference' in core),
    ("Read-only Steam probe NEVER calls an update API",
     "SteamUGC.StartItemUpdate" not in probe and
     "SteamUGC.SubmitItemUpdate" not in probe and
     "SteamUGC.SetItemMetadata(" not in probe and
     "SteamUGC.DeleteItem(" not in probe),
    ("Read-only probe distinguishes metadata FALSE from zero bytes",
     "if (!x.MetadataCallSucceeded)" in probe_policy and
     "if (byteCount == 0)" in probe_policy),
    ("Read-only probe never includes raw metadata in shareable report",
     "rawMetadata = " not in probe and "RawDeveloperMetadata =" not in probe and
     "metadataSha256 = assessment.MetadataSha256" in probe),
    ("Read-only probe reports query flags and metadata call result",
     "getQueryUGCMetadataReturnedTrue = metadataOk" in probe and
     "responseWasCached = cached" in probe and
     "returnedItemMatches = returnedId == id" in probe),
    ("Probe safe-report message does not claim nonexistent private backups",
     'Read-only probe report copied. No original/candidate metadata JSON was created by this probe.' in gui),
    ("Probe UI never unlocks repair or recovery",
     "SteamReadProbe.Run(id)" in gui and
     '_current = null;' in gui and
     "_probe.Enabled = !active;" in gui),
    ("Fixture creator requires a durable one-shot ledger",
     "FileMode.CreateNew" in creator and "TestFixturePolicy.MayCreate" in creator),
    ("Fixture creation has exact opt-in in default GUI",
     'FixtureLabEnabled = args.Length == 1' in gui and
     '"--fixture-lab"' in gui),
    ("Fixture creation confirmation is exact",
     "string.Equals(typed, Confirmation, StringComparison.Ordinal)" in fixture),
    ("Oversized-other diagnosis checks actual description contribution",
     "candidate = root.DeepClone()" in core and
     'candidate["$Value"]!["FullMetadata"]!["BaseMetadata"]!["Description"] = "";' in core and
     "Encoding.UTF8.GetByteCount(candidate.ToJsonString()) < SteamLimitBytes" in core),
    ("E2E oversized-other preserves real ModHUB description",
     'hugeOther["ExtraOversizedField"]' in disposable and
     "hugeOther[\"$Value\"]!" not in disposable),
    ("E2E scenario preparation failure explicitly reports no Steam writes",
     "PRECHECK FAILED" in e2e_runner and
     "NO STEAM METADATA UPDATE WAS SENT" in e2e_runner),
    ("Real ModHUB E2E refuses production item and fake project",
     "3810512193UL" in disposable and
     "RequiredTitlePrefix" in disposable and
     'originalProjectVrgproj' in disposable and
     'projectId["Guid"]' in disposable),
    ("Real E2E stores original and project before first mutation",
     "e2e-original-PRIVATE.json" in e2e_runner and
     "e2e-project-PRIVATE.vrgproj" in e2e_runner and
     "DisposableE2EEngine.Run(" in e2e_runner),
    ("Real E2E uses only Steam metadata setter and no public Workshop setters",
     "SteamUGC.SetItemMetadata(" in e2e_runner and
     not any(x in e2e_runner for x in (
         "SteamUGC.SetItemContent(", "SteamUGC.SetItemTitle(",
         "SteamUGC.SetItemDescription(", "SteamUGC.SetItemTags(",
         "SteamUGC.SetItemVisibility(", "SteamUGC.DeleteItem("))),
    ("Real E2E reruns the actual recovery and write preflight",
     "RecoveryReadOnlySession.Prepare(" in engine and
     "RecoveryWritePreflight.Authorize(" in engine),
    ("Real E2E checks baseline after every mutation and restricts rollback",
     'AssertState(scenario.Raw, "fault readback")' in engine and
     'RestoreKnownInjected(scenario.Raw)' in engine and
     'AssertState(baseline, "restored readback")' in engine),
    ("Real E2E requires second explicit user approval and fixture lab",
     '"RUN FULL E2E " + _id' in e2e_dialog and
     "MessageBoxDefaultButton.Button2" in e2e_dialog and
     "Program.FixtureLabEnabled" in gui and
     "new DisposableE2EForm(selectedId)" in gui),
    ("Original oversized one-field repair still present", 'JsonNode.DeepEquals(expected, edited)' in legacy),
    ("Template refuses unreviewed keys", 'CheckKnownTemplateKeys(template);' in core and
     'Unreviewed metadata key:' in core),
    ("Original identity is enforced", 'ReadOriginalProject(originalVrgproj, itemId)' in core),
    ("Owner is checked in recovery preview and fresh query session",
     'initiallyRead.Owner != owner' in preview and
     'if (data.OwnerId == 0 || data.OwnerId != expectedOwner)' in session and
     'SteamUser.GetSteamID().m_SteamID' in preview),
    ("Windows DPI regression: status, UGC probe, and summary use separate rows",
     'statusArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 30))' in gui and
     'statusArea.Controls.Add(_status, 0, 0)' in gui and
     'statusArea.Controls.Add(diagnosticButtons, 0, 1)' in gui and
     'statusBounds.Bottom > probeBounds.Top' in gui and
     'probeBounds.Bottom > statsBounds.Top' in gui),
    ("New owner-only recovery wording replaces stale preview-only claim",
     "Only the original oversized-description patch can change Steam." not in gui and
     "Owner-only recovery can restore verified backups" in gui),
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
