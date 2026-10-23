// Shared metadata-only Steam writer. The developer fixture interface is not present
// in the normal consumer build; owner/identity checks are unchanged.
using Steamworks;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace SE2WorkshopUGCFix;

internal sealed class OwnerOnlyMetadataTransport
#if SE2_INTERNAL_LAB
    : IDisposableUgcTransport
#endif
{
    private readonly ulong _id;
    private readonly ulong _owner;
    internal OwnerOnlyMetadataTransport(ulong id, ulong owner) {
        _id = id;
        _owner = owner;
    }

    public RemoteInfo ReadFresh(ulong itemId)
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

#if SE2_INTERNAL_LAB
    DisposableRemote IDisposableUgcTransport.ReadFresh(ulong itemId)
    {
        var fresh = ReadFresh(itemId);
        return new DisposableRemote(fresh.Raw, fresh.Owner);
    }

    public void SubmitMetadataOnly(ulong itemId, string raw)
        => SubmitCore(itemId, raw, null,
            "SE2 UGC E2E disposable test: metadata fault / restoration");
#endif

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
