// Pure classification of Steam's ACTUAL query and metadata-call outcomes.
// No Steamworks references, no file I/O, no write operations.
using System;
using System.Security.Cryptography;
using System.Text;

namespace SE2WorkshopUGCFix;

internal enum SteamProbeStatus {
    QueryFailed, CachedQuery, UnexpectedItem, WrongOwner,
    MetadataCallFailed, AmbiguousNull, ConfirmedEmpty, Present
}
internal sealed record SteamProbeObservation(
    ulong RequestedItemId, ulong ReturnedItemId, uint AppId, ulong OwnerId,
    ulong SignedInSteamId, bool DetailsQuerySucceeded, bool WasCached,
    bool MetadataCallSucceeded, string? RawDeveloperMetadata);

internal sealed record SteamProbeAssessment(
    SteamProbeStatus Status, int? MetadataBytes, string? MetadataSha256,
    bool OwnedBySignedInUser, bool EligibleForRecoveryPreview, string Explanation);

internal static class SteamMetadataProbePolicy
{
    internal static SteamProbeAssessment Evaluate(SteamProbeObservation x)
    {
        bool owned = x.OwnerId != 0 && x.OwnerId == x.SignedInSteamId;
        SteamProbeAssessment Result(SteamProbeStatus kind, string why, int? bytes = null, string? hash = null,
            bool mayPreview = false) => new(kind, bytes, hash, owned, mayPreview, why);

        if (!x.DetailsQuerySucceeded)
            return Result(SteamProbeStatus.QueryFailed, "Steam UGC details query failed. Missing metadata NOT established.");
        if (x.WasCached)
            return Result(SteamProbeStatus.CachedQuery, "Steam returned cached details. Rerun a fresh query.");
        if (x.AppId != 1133870 || x.RequestedItemId == 0 || x.ReturnedItemId != x.RequestedItemId)
            return Result(SteamProbeStatus.UnexpectedItem, "Steam application/Workshop ID mismatch; no recovery.");
        if (!owned)
            return Result(SteamProbeStatus.WrongOwner, "Workshop owner is not the signed-in Steam account.");
        if (!x.MetadataCallSucceeded)
            return Result(SteamProbeStatus.MetadataCallFailed,
                "GetQueryUGCMetadata returned FALSE. This is UNKNOWN, NOT proven empty developer metadata.");
        if (x.RawDeveloperMetadata is null)
            return Result(SteamProbeStatus.AmbiguousNull,
                "GetQueryUGCMetadata returned TRUE but no string. Treat as UNKNOWN until independently verified.");

        int byteCount = Encoding.UTF8.GetByteCount(x.RawDeveloperMetadata);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(x.RawDeveloperMetadata)));
        if (byteCount == 0)
            return Result(SteamProbeStatus.ConfirmedEmpty,
                "Metadata API returned TRUE with an empty UTF-8 string. Confirmed 0 bytes in this probe.",
                0, hash, true);
        return Result(SteamProbeStatus.Present,
            "Steam returned developer metadata. Use the normal CHECK METADATA classification.",
            byteCount, hash);
    }
}
