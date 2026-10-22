// E2E Steam transport is intentionally *metadata only*. No Workshop title,
// description, visibility, tags, image, dependencies or file/content changes.
using Steamworks;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace SE2WorkshopUGCFix;

internal sealed class OwnerOnlyMetadataTransport : IDisposableUgcTransport
{
    private readonly ulong _id;
    private readonly ulong _owner;
    internal OwnerOnlyMetadataTransport(ulong id, ulong owner) {
        _id = id;
        _owner = owner;
    }

    public DisposableRemote ReadFresh(ulong itemId)
    {
        if (itemId != _id) throw new InvalidOperationException("Different Workshop item requested.");
        RemoteInfo item = SteamFix.ReadRemote(_id);
        if (_owner == 0 || item.Owner != _owner ||
            SteamUser.GetSteamID().m_SteamID != _owner)
            throw new InvalidOperationException("Original Steam owner changed.");
        return new(item.Raw, item.Owner);
    }

    // Production recovery uses the same proven metadata-only Steam transport as
    // the disposable batch, with one last byte-exact optimistic concurrency gate.
    internal void SubmitMetadataOnlyIfUnchanged(ulong itemId, string expectedCurrentRaw, string raw)
        => SubmitCore(itemId, raw, expectedCurrentRaw,
            ""); // Avoid attaching a public change-log message to a recovery.

    public void SubmitMetadataOnly(ulong itemId, string raw)
        => SubmitCore(itemId, raw, null,
            "SE2 UGC E2E disposable test: metadata fault / restoration");

    private void SubmitCore(
        ulong itemId, string raw, string? expectedCurrentRaw, string changeNote)
    {
        if (itemId != _id || _owner == 0 || SteamUser.GetSteamID().m_SteamID != _owner)
            throw new InvalidOperationException("Steam owner/item mismatch: no submit.");
        if (Encoding.UTF8.GetByteCount(raw) >= UniversalRecovery.SteamLimitBytes)
            throw new InvalidOperationException("Developer metadata exceeds Steam byte limit.");
        // Fresh owner + expected raw bytes verification immediately before
        // starting a metadata-only update. No blind retries of uncertain calls.
        var beforeSubmit = ReadFresh(_id);
        if (expectedCurrentRaw is not null && beforeSubmit.Raw != expectedCurrentRaw)
            throw new InvalidOperationException(
                "Steam developer metadata changed after authorization. NO UPDATE SENT.");
        var handle = SteamUGC.StartItemUpdate(new AppId_t(1133870), new PublishedFileId_t(_id));
        if (handle == UGCUpdateHandle_t.Invalid)
            throw new InvalidOperationException("StartItemUpdate returned invalid handle.");
        if (!SteamUGC.SetItemMetadata(handle, raw))
            throw new InvalidOperationException("SetItemMetadata rejected payload; no submission.");

        // SetItemMetadata is the ONLY setter invoked here.
        var call = SteamUGC.SubmitItemUpdate(handle, changeNote);
        if (call == SteamAPICall_t.Invalid)
            throw new InvalidOperationException("SubmitItemUpdate returned invalid handle; outcome uncertain.");
        SubmitItemUpdateResult_t? reply = null;
        bool ioFailed = false;
        var callback = CallResult<SubmitItemUpdateResult_t>.Create((data, failed) => {
            reply = data; ioFailed = failed;
        });
        callback.Set(call);
        var watch = Stopwatch.StartNew();
        while (reply is null && watch.Elapsed < TimeSpan.FromSeconds(120)) {
            SteamAPI.RunCallbacks();
            Thread.Sleep(100);
        }
        if (reply is null)
            throw new TimeoutException("Steam update timed out; outcome UNKNOWN. Inspect before any retry.");
        var outcome = reply.Value;
        if (ioFailed || outcome.m_eResult != EResult.k_EResultOK ||
            outcome.m_nPublishedFileId.m_PublishedFileId != _id ||
            outcome.m_bUserNeedsToAcceptWorkshopLegalAgreement)
            throw new InvalidOperationException(
                "Steam did not confirm metadata-only submit. Outcome requires inspection: " +
                outcome.m_eResult + "; IO-failed=" + ioFailed);
    }
}

internal sealed record DisposableE2ERunReceipt(
    ulong ItemId, bool Complete, bool OriginalRestored, string BackupDirectory, string SummaryFile,
    DisposableE2EStep[] Steps);

internal static class DisposableE2ERunService
{
    internal static DisposableE2ERunReceipt Run(ulong id, string vrgprojPath, Action<string>? stage = null)
        => SteamFix.WithSteam(() => RunConnected(id, vrgprojPath, stage));

    private static DisposableE2ERunReceipt RunConnected(ulong id, string vrgprojPath, Action<string>? stage)
    {
        if (string.IsNullOrWhiteSpace(vrgprojPath) || !File.Exists(vrgprojPath) ||
            new FileInfo(vrgprojPath).Length > 131072)
            throw new ArgumentException("Select the real original ModHUB .vrgproj under 128 KiB.");
        string project = File.ReadAllText(vrgprojPath, new UTF8Encoding(false, true));
        ulong owner = SteamUser.GetSteamID().m_SteamID;
        if (owner == 0) throw new InvalidOperationException("Steam login required.");
        RemoteInfo first = SteamFix.ReadRemote(id);
        DisposableTestManifest manifest;
        try {
            // All five scenarios are prepared BEFORE the first Steam mutation.
            manifest = DisposableModScenarios.Prepare(id, owner, first.Owner, project, first.Raw);
        } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) {
            throw new InvalidOperationException(
                "PRECHECK FAILED — NO STEAM METADATA UPDATE WAS SENT. " + ex.Message, ex);
        }

        string directory = SteamFix.NewBackupFolder(id);
        string pointer = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SE2WorkshopUGCFix", "e2e-latest-backup-" + id + ".txt");
        File.WriteAllText(pointer, directory, new UTF8Encoding(false));
        string baselineFile = Path.Combine(directory, "e2e-original-PRIVATE.json");
        File.WriteAllText(baselineFile, manifest.OriginalRaw, new UTF8Encoding(false));
        if (File.ReadAllText(baselineFile, new UTF8Encoding(false, true)) != manifest.OriginalRaw)
            throw new InvalidOperationException("Cannot independently read back original backup. No Steam writes.");

        // Save project for exact identity recovery without overwriting the user's source.
        File.WriteAllText(Path.Combine(directory, "e2e-project-PRIVATE.vrgproj"), project,
            new UTF8Encoding(false));
        if (!File.Exists(Path.Combine(directory, "e2e-project-PRIVATE.vrgproj")))
            throw new InvalidOperationException("Project backup missing.");

        stage?.Invoke("Original metadata and real ModHUB project backed up locally.");
        var result = DisposableE2EEngine.Run(manifest,
            new OwnerOnlyMetadataTransport(id, owner), stage);

        string report = Path.Combine(directory, "e2e-report-SAFE-TO-SHARE.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new {
            mode = "DISPOSABLE_MOD_E2E",
            appId = 1133870,
            workshopItem = id,
            startedFromRealModhubProject = true,
            dateUtc = DateTimeOffset.UtcNow,
            originalSha256 = result.OriginalSha256,
            complete = result.Complete,
            originalRestored = result.OriginalRestored,
            steps = result.Steps,
            steamWritesLimitedToMetadata = true,
            publicWorkshopDescriptionUpdatedByTool = false,
            contentUpdatedByTool = false,
            alert = result.OriginalRestored ? "Original metadata read-back verified." :
                "STOP. Original state NOT confirmed restored. Preserve private backup and inspect."
        }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return new(id, result.Complete, result.OriginalRestored, directory, report,
            result.Steps.ToArray());
    }
}

// Emergency recovery for a *known injected* fault on the test item only.
// Never infer arbitrary "old" identities, and never overwrite a changed item.
internal static class DisposableE2EManualRescue
{
    internal static string Restore(ulong id, string backupDirectory, string typedApproval)
        => SteamFix.WithSteam(() => RestoreConnected(id, backupDirectory, typedApproval));

    private static string RestoreConnected(ulong id, string directory, string approval)
    {
        if (approval != "RESTORE TEST " + id)
            throw new InvalidOperationException("Exact RESTORE TEST <ID> confirmation required.");
        string root = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SE2WorkshopUGCFix", "Backups")) + Path.DirectorySeparatorChar;
        string selected = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!selected.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Backup is not in the application's private backup directory.");

        string originalPath = Path.Combine(selected, "e2e-original-PRIVATE.json");
        string projectPath = Path.Combine(selected, "e2e-project-PRIVATE.vrgproj");
        if (!File.Exists(originalPath) || !File.Exists(projectPath))
            throw new InvalidOperationException("Both original metadata and ModHUB project backups are required.");
        string original = File.ReadAllText(originalPath, new UTF8Encoding(false, true));
        string project = File.ReadAllText(projectPath, new UTF8Encoding(false, true));
        ulong owner = SteamUser.GetSteamID().m_SteamID;
        if (owner == 0) throw new InvalidOperationException("Steam owner not signed in.");
        // Independently recheck original real mod/project identity.
        var manifest = DisposableModScenarios.Prepare(id, owner, owner, project, original);
        var transport = new OwnerOnlyMetadataTransport(id, owner);
        string current = transport.ReadFresh(id).Raw;
        if (current == original) return "Original already restored: SHA256 " + SteamFix.Hash(original);

        var knownFault = manifest.Scenarios.FirstOrDefault(x =>
            x.MayWriteToDisposableItem && x.Raw == current);
        if (knownFault is null)
            throw new InvalidOperationException(
                "Current Steam metadata differs from all known test injections. STOP without overwriting.");
        transport.SubmitMetadataOnly(id, original);
        if (transport.ReadFresh(id).Raw != original)
            throw new InvalidOperationException("Restore submitted but fresh readback did not match. STOP.");
        return "RESTORED test item " + id + " from private same-item backup. SHA256: " +
            SteamFix.Hash(original);
    }
}
