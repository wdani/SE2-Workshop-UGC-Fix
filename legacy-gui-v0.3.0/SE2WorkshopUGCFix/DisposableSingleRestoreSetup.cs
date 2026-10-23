// One intentionally broken state for a *single* real GUI RESTORE acceptance
// test. Not part of the ordinary user app; only the explicit fixture lab calls it.
// Reuses the exact project identity checks and emergency-rescue backup format
// of the previously validated full disposable E2E, but DOES NOT auto-restore.
// This is a controlled developer test, never for production Workshop items.
using Steamworks;
using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SE2WorkshopUGCFix;

internal sealed record SingleRestorePreparation(
    ulong ItemId, string BackupDirectory, string OriginalBackupFile,
    string OriginalSha256, string InjectedScenario);

internal static class DisposableSingleRestoreSetup
{
    internal static SingleRestorePreparation Prepare(
        ulong itemId, string originalVrgprojPath)
        => SteamFix.WithSteam(() => PrepareConnected(itemId, originalVrgprojPath));

    private static SingleRestorePreparation PrepareConnected(
        ulong itemId, string originalVrgprojPath)
    {
        if (string.IsNullOrWhiteSpace(originalVrgprojPath) ||
            !File.Exists(originalVrgprojPath) ||
            new FileInfo(originalVrgprojPath).Length > 131072)
            throw new ArgumentException("Choose the original, published disposable .vrgproj.");

        string project = File.ReadAllText(originalVrgprojPath, new UTF8Encoding(false, true));
        ulong owner = SteamUser.GetSteamID().m_SteamID;
        if (owner == 0) throw new InvalidOperationException("Steam login required.");

        var before = SteamFix.ReadRemote(itemId);
        // An actual ModHUB project, title, item ID, owner, Steam binding and
        // project GUID MUST all agree. This rejects existing production items.
        var manifest = DisposableModScenarios.Prepare(
            itemId, owner, before.Owner, project, before.Raw);
        var one = manifest.Scenarios.Single(x => x.Name == "empty-metadata");
        if (!one.MayWriteToDisposableItem || one.Expected != MetadataCondition.Missing)
            throw new InvalidOperationException("Only the known zero-byte scenario is permitted.");

        string folder = SteamFix.NewBackupFolder(itemId);
        string original = Path.Combine(folder, "e2e-original-PRIVATE.json");
        string projectBackup = Path.Combine(folder, "e2e-project-PRIVATE.vrgproj");
        string pointer = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SE2WorkshopUGCFix", "e2e-latest-backup-" + itemId + ".txt");
        var encoding = new UTF8Encoding(false);
        File.WriteAllText(original, manifest.OriginalRaw, encoding);
        File.WriteAllText(projectBackup, project, encoding);
        if (File.ReadAllText(original, new UTF8Encoding(false, true)) != manifest.OriginalRaw ||
            File.ReadAllText(projectBackup, new UTF8Encoding(false, true)) != project)
            throw new InvalidOperationException("Private originals could not be read back. NO UPDATE.");

        // Maintain compatibility with the lab's existing guarded emergency
        // restore. Always write the rescue pointer BEFORE the first Steam call.
        File.WriteAllText(pointer, folder, encoding);
        var writer = new OwnerOnlyMetadataTransport(itemId, owner);
        if (writer.ReadFresh(itemId).Raw != manifest.OriginalRaw)
            throw new InvalidOperationException(
                "Workshop metadata changed during preparation. NO UPDATE.");

        // Exactly one mutation: successful Steam 0-byte developer metadata.
        // This deliberately leaves the disposable item broken for the
        // ordinary GUI's own separately-confirmed RESTORE button.
        try {
            writer.SubmitMetadataOnlyIfUnchanged(
                itemId, manifest.OriginalRaw, one.Raw);
            if (writer.ReadFresh(itemId).Raw != one.Raw)
                throw new InvalidOperationException(
                    "Steam submission returned but exact injected state was not confirmed.");
        } catch (Exception e) {
            throw new InvalidOperationException(
                "SINGLE TEST STOP / REMOTE STATE UNCERTAIN. Do not retry blindly. " +
                "The private backup remains at: " + original +
                ". Use the fixture lab's EMERGENCY RESTORE only after inspecting Steam. " +
                e.Message, e);
        }

        var result = new SingleRestorePreparation(
            itemId, folder, original, SteamFix.Hash(manifest.OriginalRaw), one.Name);
        var safeReport = new {
            mode = "SINGLE_GUI_RESTORE_PREPARATION",
            itemId,
            dateUtc = DateTimeOffset.UtcNow,
            scenario = one.Name,
            originalSha256 = result.OriginalSha256,
            knownInjectedStateReadbackConfirmed = true,
            originalBackedUpBeforeSteamWrite = true,
            originalRestored = false,
            next = "Close fixture lab, run normal GUI CHECK METADATA then RECOVERY PREVIEW with original backup, approve RESTORE. Emergency rescue exists in fixture lab.",
            noPublicContentSettersUsed = true
        };
        File.WriteAllText(
            Path.Combine(folder, "single-restore-prepare-SAFE-TO-SHARE.json"),
            JsonSerializer.Serialize(safeReport, new JsonSerializerOptions { WriteIndented = true }),
            encoding);
        return result;
    }
}
