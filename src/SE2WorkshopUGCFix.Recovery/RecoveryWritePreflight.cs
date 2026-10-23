// Pure guard for a FUTURE user-authorized metadata-only write.
// There are deliberately NO Steamworks references, file writes, or submit calls.
// Returning an authorization DOES NOT submit it; the live transport is NOT wired.
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace SE2WorkshopUGCFix;

internal sealed record RecoveryWriteAuthorization(
    ulong ItemId, ulong OwnerId, RecoverySource Source,
    string CandidateJson, string CandidateSha256, string OriginalSha256,
    string? ReferenceSha256);

// Safety decision only. The caller must provide privately stored backup text,
// not merely a reported hash. A future transport MUST repeat these checks
// immediately before any future metadata-only Steam submission on the original item.
internal static class RecoveryWritePreflight
{
    internal static RecoveryWriteAuthorization Authorize(
        VerifiedRecoveryPreview preview, ulong signedInSteamId,
        Func<ulong, RecoveryRemoteSnapshot> readFresh,
        string storedOriginalBackup, string storedCandidateBackup,
        string? storedReferenceBackup,
        string typedConfirmation)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(readFresh);
        if (preview.ItemId == 0 || preview.OwnerId == 0 ||
            signedInSteamId == 0 || preview.OwnerId != signedInSteamId ||
            preview.Plan.ItemId != preview.ItemId)
            throw new InvalidOperationException("Owner or Workshop item changed. Authorization denied.");

        if (!string.Equals(typedConfirmation, "RESTORE " + preview.ItemId,
            StringComparison.Ordinal))
            throw new InvalidOperationException("Exact RESTORE <WorkshopID> confirmation required.");

        if (!preview.Plan.EligibleForApply ||
            preview.Plan.Source is not (RecoverySource.OriginalBackup or RecoverySource.WorkingItem))
            throw new InvalidOperationException("Experimental/generic recovery is PREVIEW ONLY.");

        if (preview.Condition is not (MetadataCondition.Missing or MetadataCondition.Malformed) ||
            UniversalRecovery.Diagnose(preview.OriginalRaw, preview.ItemId).Condition != preview.Condition)
            throw new InvalidOperationException("Original preview does not describe missing/malformed metadata.");

        if (storedOriginalBackup != preview.OriginalRaw ||
            Hash(storedOriginalBackup) != preview.OriginalSha256)
            throw new InvalidOperationException("Private ORIGINAL backup is missing or modified.");

        if (storedCandidateBackup != preview.Plan.CandidateJson ||
            Hash(storedCandidateBackup) != preview.CandidateSha256 ||
            Encoding.UTF8.GetByteCount(storedCandidateBackup) != preview.Plan.CandidateBytes ||
            preview.Plan.CandidateBytes <= 0 ||
            preview.Plan.CandidateBytes >= UniversalRecovery.SteamLimitBytes ||
            UniversalRecovery.Diagnose(storedCandidateBackup, preview.ItemId).Condition != MetadataCondition.Healthy)
            throw new InvalidOperationException("Private CANDIDATE backup does not match a valid bounded preview.");

        var candidate = JsonNode.Parse(storedCandidateBackup)
            ?? throw new InvalidOperationException("Invalid candidate JSON.");
        var value = candidate["$Value"] ?? throw new InvalidOperationException("Missing candidate $Value");
        if (value["Author"]?.GetValue<ulong>() != signedInSteamId ||
            !string.Equals(value["FullMetadata"]?["ProjectID"]?["Guid"]?.GetValue<string>(),
                preview.Plan.ProjectGuid, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Candidate Steam author or original ProjectGUID mismatch.");

        if (preview.Plan.Source == RecoverySource.WorkingItem) {
            if (preview.ReferenceItemId is not ulong referenceId ||
                referenceId == 0 || referenceId == preview.ItemId ||
                preview.ReferenceRaw is null || preview.ReferenceSha256 is null ||
                storedReferenceBackup != preview.ReferenceRaw ||
                Hash(storedReferenceBackup) != preview.ReferenceSha256 ||
                UniversalRecovery.Diagnose(storedReferenceBackup, referenceId).Condition != MetadataCondition.Healthy)
                throw new InvalidOperationException("Verified owner-matched reference or its private backup is missing.");

            string liveReference = RecoveryReadOnlySession.CheckFresh(
                readFresh(referenceId), referenceId, signedInSteamId);
            if (liveReference != preview.ReferenceRaw)
                throw new InvalidOperationException("Reference item changed since preview. Authorization denied.");
        } else if (preview.ReferenceItemId.HasValue || preview.ReferenceRaw is not null ||
                   preview.ReferenceSha256 is not null || storedReferenceBackup is not null) {
            throw new InvalidOperationException("An exact-item backup must not import comparison metadata.");
        }

        string liveOriginal = RecoveryReadOnlySession.CheckFresh(
            readFresh(preview.ItemId), preview.ItemId, signedInSteamId);
        if (liveOriginal != preview.OriginalRaw ||
            UniversalRecovery.Diagnose(liveOriginal, preview.ItemId).Condition != preview.Condition)
            throw new InvalidOperationException("Original Workshop metadata changed since preview.");

        return new RecoveryWriteAuthorization(preview.ItemId, signedInSteamId,
            preview.Plan.Source, storedCandidateBackup, preview.CandidateSha256,
            preview.OriginalSha256, preview.ReferenceSha256);
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
