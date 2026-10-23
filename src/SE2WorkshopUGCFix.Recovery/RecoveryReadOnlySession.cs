// Steam-independent read-only session guard. This file CANNOT write to Steam.
// The caller supplies an explicitly fresh, successful read-only metadata reader.
using System;
using System.Security.Cryptography;
using System.Text;

namespace SE2WorkshopUGCFix;

internal sealed record RecoveryRemoteSnapshot(
    ulong ItemId, uint AppId, ulong OwnerId,
    bool QuerySucceeded, bool IsCached, string? DeveloperMetadata);

internal sealed record VerifiedRecoveryPreview(
    ulong ItemId, ulong OwnerId, ulong? ReferenceItemId, MetadataCondition Condition,
    RecoveryPlan Plan, string OriginalRaw, string OriginalSha256,
    string CandidateSha256, string? ReferenceRaw, string? ReferenceSha256);

internal static class RecoveryReadOnlySession
{
    internal const uint ExpectedAppId = 1133870;

    // No file I/O; no side effects. If any remote state is inconsistent, throw
    // before returning a candidate. GUI remains responsible for private backups.
    internal static VerifiedRecoveryPreview Prepare(
        ulong itemId,
        ulong signedInSteamId,
        Func<ulong, RecoveryRemoteSnapshot> readFresh,
        string? oldDeveloperMetadataBackup,
        string? originalVrgproj,
        string? originalModhubMetadata,
        ulong? workingItemId)
    {
        ArgumentNullException.ThrowIfNull(readFresh);
        if (itemId == 0 || signedInSteamId == 0)
            throw new ArgumentException("Original item ID and signed-in owner must be known.");

        var original = CheckFresh(readFresh(itemId), itemId, signedInSteamId);
        var state = UniversalRecovery.Diagnose(original, itemId).Condition;
        if (state is not (MetadataCondition.Missing or MetadataCondition.Malformed))
            throw new InvalidOperationException(
                "Recovery not appropriate for current state: " + state + ". Use the diagnosis first.");

        RecoveryPlan plan;
        string? referenceRaw = null;
        string? referenceHash = null;

        if (oldDeveloperMetadataBackup is not null) {
            if (workingItemId.HasValue)
                throw new ArgumentException("Choose an original backup OR a working reference, not both.");
            plan = UniversalRecovery.FromOriginalBackup(
                itemId, signedInSteamId, signedInSteamId,
                original, oldDeveloperMetadataBackup);
        } else {
            if (string.IsNullOrWhiteSpace(originalVrgproj))
                throw new ArgumentException("Original .vrgproj required without a full backup.");
            if (workingItemId.HasValue) {
                ulong referenceId = workingItemId.Value;
                if (referenceId == 0 || referenceId == itemId)
                    throw new ArgumentException("Reference Workshop item must be different and nonzero.");
                referenceRaw = CheckFresh(readFresh(referenceId), referenceId, signedInSteamId);
                if (UniversalRecovery.Diagnose(referenceRaw, referenceId).Condition != MetadataCondition.Healthy)
                    throw new InvalidOperationException("Working reference metadata must be healthy.");
                referenceHash = Hash(referenceRaw);
                plan = UniversalRecovery.FromWorkingItem(
                    itemId, signedInSteamId, signedInSteamId, signedInSteamId,
                    original, originalVrgproj, originalModhubMetadata, referenceRaw);
            } else {
                plan = UniversalRecovery.FromGenericReference(
                    itemId, originalVrgproj, originalModhubMetadata,
                    signedInSteamId, signedInSteamId);
            }
        }

        // A new uncached query must still match both sources exactly.
        string after = CheckFresh(readFresh(itemId), itemId, signedInSteamId);
        if (after != original)
            throw new InvalidOperationException("Original remote metadata changed during preview; STOP.");
        if (workingItemId.HasValue) {
            string newReference = CheckFresh(readFresh(workingItemId.Value),
                workingItemId.Value, signedInSteamId);
            if (newReference != referenceRaw)
                throw new InvalidOperationException("Reference metadata changed during preview; STOP.");
        }

        return new VerifiedRecoveryPreview(
            itemId, signedInSteamId, workingItemId, state, plan, original, Hash(original),
            Hash(plan.CandidateJson), referenceRaw, referenceHash);
    }

    // Called only with the transport's actual query status. Distinguishes:
    // - query failed, even if the API sets metadata to null or "";
    // - successful, uncached, exactly empty ("") metadata, which is Missing;
    // - cached/stale results, wrong app/owner/item.
    internal static string CheckFresh(
        RecoveryRemoteSnapshot data, ulong expectedId, ulong expectedOwner)
    {
        if (!data.QuerySucceeded)
            throw new InvalidOperationException("Steam metadata QUERY FAILED; it is NOT confirmed empty.");
        if (data.IsCached)
            throw new InvalidOperationException("Steam returned CACHED metadata; require fresh response.");
        if (data.AppId != ExpectedAppId || data.ItemId != expectedId)
            throw new InvalidOperationException("Steam app or Workshop item identity mismatch.");
        if (data.OwnerId == 0 || data.OwnerId != expectedOwner)
            throw new InvalidOperationException("Steam owner does not match signed-in account.");
        return data.DeveloperMetadata ??
            throw new InvalidOperationException("Steam query succeeded but returned null, not a confirmed 0-byte string.");
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
