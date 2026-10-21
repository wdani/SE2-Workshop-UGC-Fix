// Explicit owner-controlled disposable test-item creation; never updates existing items.
// Only CreateItem is used. No content, title, visibility, or developer metadata is sent.
using Steamworks;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace SE2WorkshopUGCFix;

internal sealed record TestItemCreationResult(
    ulong ItemId, string SteamResult, bool NeedsWorkshopAgreement, string ReceiptPath);

internal static class TestItemCreator
{
    // This writes ONLY a local attempt/receipt. Creating the new item itself is a
    // real Steam account mutation and must follow the in-app typed confirmation.
    internal static TestItemCreationResult Create(string typedConfirmation) =>
        SteamFix.WithSteam(() => CreateConnected(typedConfirmation));

    private static TestItemCreationResult CreateConnected(string typed)
    {
        if (!TestFixturePolicy.MayCreate(typed, File.Exists(TestFixturePolicy.LedgerPath)))
            throw new InvalidOperationException(
                "Creation blocked. Exact confirmation required and only ONE attempt permitted. " +
                "If an attempt already exists, review the local ledger; do not retry blindly.");
        ulong owner = SteamUser.GetSteamID().m_SteamID;
        if (owner == 0) throw new InvalidOperationException("Steam user is not logged in.");

        Directory.CreateDirectory(TestFixturePolicy.LedgerDirectory);
        // Reserve the attempt *before* sending any Steam request. CreateNew
        // also prevents concurrent windows from creating a second test item.
        using (var ledger = new FileStream(TestFixturePolicy.LedgerPath,
            FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(ledger, new UTF8Encoding(false))) {
            writer.Write(JsonSerializer.Serialize(new {
                appId = TestFixturePolicy.AppId,
                dateUtc = DateTimeOffset.UtcNow,
                status = "ATTEMPT RESERVED - outcome unknown until creation-result.json exists",
                accountIdStored = false,
                existingItemsModified = false,
                setItemMetadataCalled = false,
                contentUploaded = false,
                automaticRetryEnabled = false
            }, new JsonSerializerOptions { WriteIndented = true }));
        }

        var call = SteamUGC.CreateItem(new AppId_t(TestFixturePolicy.AppId),
            EWorkshopFileType.k_EWorkshopFileTypeCommunity);
        if (call == SteamAPICall_t.Invalid)
            throw new InvalidOperationException(
                "Steam returned invalid CreateItem handle. Outcome UNKNOWN. " +
                "Do not retry; review Steam account and local attempt record.");

        CreateItemResult_t? finished = null;
        bool ioFailure = false;
        var callback = CallResult<CreateItemResult_t>.Create((result, failed) => {
            finished = result;
            ioFailure = failed;
        });
        callback.Set(call);
        var watch = Stopwatch.StartNew();
        while (finished is null && watch.Elapsed < TimeSpan.FromSeconds(120)) {
            SteamAPI.RunCallbacks();
            Thread.Sleep(100);
        }

        if (finished is null)
            throw new TimeoutException(
                "Steam item creation timed out. OUTCOME UNKNOWN. A new item may already exist. " +
                "Do not retry. Inspect your created items on Steam.");

        var reply = finished.Value;
        ulong itemId = reply.m_nPublishedFileId.m_PublishedFileId;
        bool succeeded = !ioFailure && reply.m_eResult == EResult.k_EResultOK && itemId != 0;
        string resultCode = reply.m_eResult.ToString();
        var receipt = new {
            createdUtc = DateTimeOffset.UtcNow,
            appId = TestFixturePolicy.AppId,
            newWorkshopItem = itemId == 0 ? (ulong?)null : itemId,
            callbackResult = resultCode,
            ioFailure,
            status = succeeded ? "CREATE CONFIRMED" : "UNCONFIRMED - NO AUTOMATIC RETRY",
            needsWorkshopLegalAgreement = reply.m_bUserNeedsToAcceptWorkshopLegalAgreement,
            existingItemsModified = false,
            workshopMetadataSubmitted = false,
            contentUploaded = false,
            publicVisibilityVerified = false,
            visibilityNote = "Visibility has NOT been set or confirmed. Do not assume private.",
            automaticRetryEnabled = false
        };
        // Never overwrite a receipt or create a second item automatically.
        using (var stream = new FileStream(TestFixturePolicy.ResultPath,
            FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) {
            writer.Write(JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (!succeeded)
            throw new InvalidOperationException(
                "Steam did not confirm new item creation (" + resultCode +
                ", IO failure=" + ioFailure + "). Outcome requires manual review; NO RETRY.");

        return new(itemId, resultCode,
            reply.m_bUserNeedsToAcceptWorkshopLegalAgreement, TestFixturePolicy.ResultPath);
    }
}
