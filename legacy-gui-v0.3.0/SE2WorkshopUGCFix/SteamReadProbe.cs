// READ-ONLY Steamworks UGC diagnostic. Never calls StartItemUpdate,
// SetItemMetadata or SubmitItemUpdate. Produces a shareable report containing
// ONLY status flags / byte count / hashes, no raw metadata or Steam account ID.
using Steamworks;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace SE2WorkshopUGCFix;

internal sealed record SteamReadProbeResult(SteamProbeAssessment Assessment, string ReportFile);

internal static class SteamReadProbe
{
    private const uint AppId = 1133870;
    private const int BufferBytes = 32768;

    internal static SteamReadProbeResult Run(ulong workshopItemId) =>
        SteamFix.WithSteam(() => RunConnected(workshopItemId));

    private static SteamReadProbeResult RunConnected(ulong id)
    {
        ulong signedIn = SteamUser.GetSteamID().m_SteamID;
        if (signedIn == 0) throw new InvalidOperationException("Sign into Steam first.");
        var handle = SteamUGC.CreateQueryUGCDetailsRequest(
            new[] { new PublishedFileId_t(id) }, 1);

        bool queryOk = false;
        bool cached = false;
        ulong returnedId = 0;
        ulong owner = 0;
        uint appId = 0;
        bool metadataOk = false;
        string? raw = null;
        string queryCode = "UNKNOWN";

        try {
            if (!SteamUGC.SetReturnMetadata(handle, true))
                throw new InvalidOperationException("SetReturnMetadata failed: no valid UGC query handle.");
            if (!SteamUGC.SetAllowCachedResponse(handle, 0))
                throw new InvalidOperationException("Could not enforce uncached UGC response.");

            SteamUGCQueryCompleted_t? completed = null;
            bool ioFailure = false;
            var callback = CallResult<SteamUGCQueryCompleted_t>.Create((data, failed) => {
                completed = data;
                ioFailure = failed;
            });
            callback.Set(SteamUGC.SendQueryUGCRequest(handle));
            var stopwatch = Stopwatch.StartNew();
            while (completed is null && stopwatch.Elapsed < TimeSpan.FromSeconds(45)) {
                SteamAPI.RunCallbacks();
                Thread.Sleep(50);
            }
            if (completed is null) throw new TimeoutException("Steam UGC query timed out. No metadata conclusion.");

            var result = completed.Value;
            cached = result.m_bCachedData;
            queryCode = result.m_eResult.ToString();
            queryOk = !ioFailure && result.m_eResult == EResult.k_EResultOK
                && result.m_unNumResultsReturned == 1;
            if (queryOk && SteamUGC.GetQueryUGCResult(handle, 0, out SteamUGCDetails_t details)) {
                queryOk = details.m_eResult == EResult.k_EResultOK;
                returnedId = details.m_nPublishedFileId.m_PublishedFileId;
                owner = details.m_ulSteamIDOwner;
                appId = details.m_nConsumerAppID.m_AppId;
                if (queryOk && !cached && returnedId == id && appId == AppId && owner == signedIn)
                    metadataOk = SteamUGC.GetQueryUGCMetadata(handle, 0, out raw, BufferBytes);
            } else queryOk = false;
        } finally {
            SteamUGC.ReleaseQueryUGCRequest(handle);
        }

        var assessment = SteamMetadataProbePolicy.Evaluate(new SteamProbeObservation(
            id, returnedId, appId, owner, signedIn, queryOk, cached, metadataOk, raw));

        string dir = SteamFix.NewBackupFolder(id);
        string report = Path.Combine(dir, "report-SAFE-TO-SHARE.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new {
            diagnosticType = "STEAM_READ_ONLY_PROBE",
            dateUtc = DateTimeOffset.UtcNow,
            appIdExpected = AppId,
            requestedWorkshopItem = id,
            detailQueryCode = queryCode,
            detailsQuerySucceeded = queryOk,
            responseWasCached = cached,
            returnedItemMatches = returnedId == id,
            consumerAppMatches = appId == AppId,
            ownedBySignedInUser = assessment.OwnedBySignedInUser,
            getQueryUGCMetadataReturnedTrue = metadataOk,
            rawMetadataOutputWasNull = raw is null,
            metadataBytes = assessment.MetadataBytes,
            metadataSha256 = assessment.MetadataSha256,
            classification = assessment.Status.ToString(),
            explanation = assessment.Explanation,
            steamUpdatesSubmitted = false
        }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return new(assessment, report);
    }
}
