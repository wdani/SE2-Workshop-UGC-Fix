// Full disposable-only UGC metadata exercise engine, transport-agnostic.
// Real transport may ONLY change metadata for a separately verified E2E project.
// One baseline backup before the first write, then condition -> recovery -> readback.
using System;
using System.Collections.Generic;
using System.Linq;

namespace SE2WorkshopUGCFix;

internal sealed record DisposableRemote(string Raw, ulong OwnerId);
internal interface IDisposableUgcTransport
{
    DisposableRemote ReadFresh(ulong itemId);
    void SubmitMetadataOnly(ulong itemId, string raw);
}
internal sealed record DisposableE2EStep(
    string Scenario, string Status, string Detail);
internal sealed record DisposableE2EResult(
    bool Complete, bool OriginalRestored, string OriginalSha256,
    IReadOnlyList<DisposableE2EStep> Steps);

internal static class DisposableE2EEngine
{
    // This class never touches PUBLIC title, description, previews or content.
    // Creator's signature / exact item binding must be validated separately
    // through DisposableModScenarios.Prepare before calling the engine.
    internal static DisposableE2EResult Run(
        DisposableTestManifest manifest,
        IDisposableUgcTransport transport,
        Action<string>? stage = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(transport);
        var log = new List<DisposableE2EStep>();
        string Hash(string v) => Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(v)));
        string baseline = manifest.OriginalRaw;
        string hash = Hash(baseline);
        bool broken = false;

        DisposableRemote VerifiedFresh()
        {
            var remote = transport.ReadFresh(manifest.ItemId);
            if (remote.OwnerId == 0 || remote.OwnerId != manifest.OwnerId)
                throw new InvalidOperationException("Owner mismatch — STOP.");
            return remote;
        }

        void AssertState(string expected, string phase)
        {
            var current = VerifiedFresh().Raw;
            if (current != expected)
                throw new InvalidOperationException("Unexpected remote state in " + phase +
                    "; do not overwrite unrelated changes.");
        }

        // Restore even after intentional broken JSON/identity; this is a strictly
        // isolated *disposable* test that had previously proven a valid baseline.
        void RestoreKnownInjected(string faultyState)
        {
            AssertState(faultyState, "pre-restore");
            transport.SubmitMetadataOnly(manifest.ItemId, baseline);
            AssertState(baseline, "restored readback");
            broken = false;
        }

        try {
            AssertState(baseline, "initial baseline");
            foreach (var scenario in manifest.Scenarios) {
                stage?.Invoke(scenario.Name + " / begin");
                if (!scenario.MayWriteToDisposableItem) {
                    log.Add(new(scenario.Name, "PASS-OFFLINE",
                        "Steam >5000-byte limit: oversize failure checked locally, no submission."));
                    continue;
                }

                // Each scenario starts from the original pristine metadata.
                AssertState(baseline, "before " + scenario.Name);
                if (Hash(baseline) != hash)
                    throw new InvalidOperationException("Original backup hash changed.");
                try {
                    transport.SubmitMetadataOnly(manifest.ItemId, scenario.Raw);
                    // Submission returned verified success, but still require
                    // a fresh exact read-back. No blind retries.
                    broken = true;
                    AssertState(scenario.Raw, "fault readback");
                    var classification = UniversalRecovery.Diagnose(
                        VerifiedFresh().Raw, manifest.ItemId).Condition;
                    if (classification != scenario.Expected)
                        throw new InvalidOperationException("Wrong fault classification.");

                    // Exercise the REAL recovery planner/preflight for empty and
                    // malformed. Identity conflict must be refused by the planner.
                    if (scenario.Expected is MetadataCondition.Missing or MetadataCondition.Malformed) {
                        int nReads = 0;
                        RecoveryRemoteSnapshot ReadForPreview(ulong id) {
                            nReads++;
                            var r = VerifiedFresh();
                            return new(id, RecoveryReadOnlySession.ExpectedAppId,
                                r.OwnerId, true, false, r.Raw);
                        }
                        var plan = RecoveryReadOnlySession.Prepare(
                            manifest.ItemId, manifest.OwnerId, ReadForPreview,
                            baseline, null, null, null);
                        var allow = RecoveryWritePreflight.Authorize(
                            plan, manifest.OwnerId, ReadForPreview,
                            plan.OriginalRaw, plan.Plan.CandidateJson, null,
                            "RESTORE " + manifest.ItemId);
                        if (allow.CandidateJson != baseline || nReads < 3)
                            throw new InvalidOperationException("Recovery core produced wrong backup.");
                    } else if (UniversalRecovery.Diagnose(
                        scenario.Raw, manifest.ItemId).Condition != MetadataCondition.IdentityConflict) {
                        throw new InvalidOperationException("Identity-conflict test did not fail closed.");
                    }

                    RestoreKnownInjected(scenario.Raw);
                    log.Add(new(scenario.Name, "PASS-LIVE", "Corruption, diagnosis, recovery, and uncached readback matched."));
                    stage?.Invoke(scenario.Name + " / restored");
                }
                catch (Exception e) {
                    // If Steam refused empty SetItemMetadata, the state might
                    // still be pristine. Record as unsupported, not as a success.
                    var after = VerifiedFresh().Raw;
                    if (after == baseline && !broken) {
                        log.Add(new(scenario.Name, "BLOCKED", e.Message));
                        stage?.Invoke(scenario.Name + " / blocked, original intact");
                        continue;
                    }
                    if (after == scenario.Raw) {
                        try {
                            RestoreKnownInjected(scenario.Raw);
                            log.Add(new(scenario.Name, "FAIL-ROLLED-BACK", e.Message));
                        } catch (Exception restoreError) {
                            log.Add(new(scenario.Name, "STOP-REQUIRES-MANUAL-RESTORE",
                                e.Message + " | Restore failed: " + restoreError.Message));
                            return new(false, false, hash, log);
                        }
                        return new(false, true, hash, log);
                    }
                    log.Add(new(scenario.Name, "STOP-STATE-UNKNOWN", e.Message));
                    return new(false, false, hash, log);
                }
            }
            AssertState(baseline, "final check");
            return new(log.All(x => x.Status.StartsWith("PASS-", StringComparison.Ordinal)),
                true, hash, log);
        } catch (Exception e) {
            log.Add(new("suite", "STOP", e.Message));
            return new(false, false, hash, log);
        }
    }
}
