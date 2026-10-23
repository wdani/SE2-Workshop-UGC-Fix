// Owner-only user recovery APPLY: guarded existing-item metadata update.
// Reuses the same live Steam transport validated by the genuine ModHUB E2E
// batch. No public Workshop setters, new-item creation, or retry loop.
using Steamworks;
using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SE2WorkshopUGCFix;

internal sealed record RecoveryApplyResult(string SafeSummary, string SafeReportPath);

internal static class RecoveryApplyService
{
    internal static RecoveryApplyResult Apply(RecoveryPreviewResult preview, string typedConfirmation)
        => SteamFix.WithSteam(() => ApplyConnected(preview, typedConfirmation));

    private static RecoveryApplyResult ApplyConnected(
        RecoveryPreviewResult shownPreview, string typedConfirmation)
    {
        ArgumentNullException.ThrowIfNull(shownPreview);
        var session = shownPreview.Session;
        if (!session.Plan.EligibleForApply ||
            session.Plan.Source is not (RecoverySource.OriginalBackup or RecoverySource.WorkingItem))
            throw new InvalidOperationException("No verified recovery source: PREVIEW ONLY.");

        ulong owner = SteamUser.GetSteamID().m_SteamID;
        if (owner == 0 || owner != session.OwnerId)
            throw new InvalidOperationException("Signed-in Steam owner has changed.");

        string directory = Path.GetFullPath(shownPreview.BackupFolder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string appRoot = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SE2WorkshopUGCFix", "Backups"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!directory.StartsWith(appRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Private recovery folder is outside the application backups.");

        string ReadPrivate(string name) {
            string path = Path.Combine(directory, name);
            if (!File.Exists(path) || new FileInfo(path).Length > 128 * 1024)
                throw new InvalidOperationException("Required private recovery file missing or oversized: " + name);
            return File.ReadAllText(path, new UTF8Encoding(false, true));
        }
        string original = ReadPrivate("original-developer-metadata-PRIVATE.json");
        string candidate = ReadPrivate("candidate-preview-PRIVATE.json");
        string? reference = session.Plan.Source == RecoverySource.WorkingItem
            ? ReadPrivate("working-reference-PRIVATE.json")
            : null;

        RecoveryRemoteSnapshot ReadFresh(ulong id) {
            RemoteInfo item = SteamFix.ReadRemote(id);
            return new RecoveryRemoteSnapshot(
                id, RecoveryReadOnlySession.ExpectedAppId, item.Owner,
                QuerySucceeded: true, IsCached: false, DeveloperMetadata: item.Raw);
        }
        var approved = RecoveryWritePreflight.Authorize(session, owner, ReadFresh,
            original, candidate, reference, typedConfirmation);

        // Source artifacts were written by the read-only preview BEFORE
        // confirmation. Require their contents to still match that preview,
        // not merely that a file with the expected name exists.
        if (session.Plan.Source == RecoverySource.OriginalBackup) {
            if (ReadPrivate("source-backup-PRIVATE.json") != approved.CandidateJson)
                throw new InvalidOperationException("Original historic backup changed since preview.");
        } else {
            if (shownPreview.ProjectSourceSha256 is null ||
                SteamFix.Hash(ReadPrivate("original-project-PRIVATE.json")) !=
                shownPreview.ProjectSourceSha256)
                throw new InvalidOperationException("Original .vrgproj changed since preview.");
        }
        if (shownPreview.LocalSourceSha256 is not null &&
            SteamFix.Hash(ReadPrivate("original-modhub-metadata-PRIVATE.json")) !=
            shownPreview.LocalSourceSha256)
            throw new InvalidOperationException("Original ModHUB metadata changed since preview.");

        var writer = new OwnerOnlyMetadataTransport(session.ItemId, session.OwnerId);
        // Only an authorized candidate for the SAME original item can be sent.
        // Writer itself re-reads current metadata and denies any drift.
        writer.SubmitMetadataOnlyIfUnchanged(approved.ItemId,
            session.OriginalRaw, approved.CandidateJson);

        // Fresh, owner-verified byte-for-byte readback of actual Steam metadata.
        var after = writer.ReadFresh(session.ItemId);
        if (after.Raw != approved.CandidateJson ||
            SteamFix.Hash(after.Raw) != approved.CandidateSha256)
            throw new InvalidOperationException(
                "Steam update returned, but exact candidate readback NOT confirmed. " +
                "STOP. Do not blindly retry; inspect the private backup and Workshop state.");

        var report = Path.Combine(directory, "report-RECOVERY-SAFE-TO-SHARE.json");
        var safe = new {
            mode = "OWNER_ONLY_METADATA_RECOVERY",
            appId = 1133870,
            workshopItem = session.ItemId,
            source = approved.Source.ToString(),
            dateUtc = DateTimeOffset.UtcNow,
            originalState = session.Condition.ToString(),
            originalBytes = Encoding.UTF8.GetByteCount(original),
            originalSha256 = approved.OriginalSha256,
            candidateBytes = Encoding.UTF8.GetByteCount(candidate),
            candidateSha256 = approved.CandidateSha256,
            freshReadbackMatched = true,
            metadataOnly = true,
            publicWorkshopDescriptionUpdatedByTool = false,
            contentUpdatedByTool = false,
            status = "STEAM READBACK VERIFIED - RESTART MODHUB / TEST SE2"
        };
        File.WriteAllText(report,
            JsonSerializer.Serialize(safe, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
        return new RecoveryApplyResult(
            "STEAM METADATA RECOVERED AND VERIFIED.\r\n" +
            "Workshop item: " + session.ItemId + "\r\n" +
            "Source: " + approved.Source + "\r\n" +
            "Candidate SHA256: " + approved.CandidateSha256 + "\r\n" +
            "No public title, description, preview, visibility or mod content changed by this tool.\r\n" +
            "Restart ModHUB and check the mod in SE2 before overwriting any saves.",
            report);
    }

}
