// Offline-only regression harness. Run on Windows: dotnet run --project tests/RecoverySafetyTests
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SE2WorkshopUGCFix;

const ulong Target = 3820098765UL;
const ulong Comparison = 3820098766UL;
const ulong Owner = 76561198000000001UL;
int count = 0;
void Expect(bool condition, string label) {
    if (!condition) throw new Exception("FAILED: " + label);
    Console.WriteLine("PASS " + label);
    count++;
}
void Refuse(Action attempt, string label) {
    try { attempt(); }
    catch (ArgumentException) { Console.WriteLine("PASS " + label); count++; return; }
    throw new Exception("FAILED: " + label);
}
void RefuseUnsafe(Action attempt, string label) {
    try { attempt(); }
    catch (Exception e) when (e is ArgumentException or InvalidOperationException) {
        Console.WriteLine("PASS " + label);
        count++;
        return;
    }
    throw new Exception("FAILED: " + label);
}
string ModId(ulong id) {
    var b = BitConverter.GetBytes(checked((uint)id));
    return "00000000-0000-0000-" + $"{b[0]:x2}{b[1]:x2}-{b[2]:x2}{b[3]:x2}00000000";
}
JsonObject Project(ulong id) => new() {
    ["$Type"] = "VRage:Keen.VRage.Core.Project.VRageProjectData",
    ["$Value"] = new JsonObject {
        ["Name"] = "Original Mod",
        ["ProjectID"] = new JsonObject {
            ["Guid"] = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            ["ModId"] = new JsonObject { ["Id"] = ModId(id), ["ServiceName"] = "Steam" },
            ["ServiceId"] = "Steam"
        }
    }
};
JsonObject Local() => new() {
    ["$Type"] = "VRage:Keen.VRage.ModHUB.Models.ModProject",
    ["$Value"] = new JsonObject {
        ["Name"] = "Original title",
        ["Version"] = "v0.028e",
        ["Tags"] = new JsonArray("ModTagBlock", "UGCModTypeTag")
    }
};
JsonObject Template(ulong id, ulong author, string desc = "") => new() {
    ["$Bundles"] = new JsonObject { ["VRage"] = "2.4.0.95" },
    ["$Type"] = "VRage:Keen.VRage.Steam.Data.SteamUgcMetadata",
    ["$Value"] = new JsonObject {
        ["Id"] = id,
        ["PreviewUrl"] = null,
        ["FullMetadata"] = new JsonObject {
            ["$Type"] = "VRage:Keen.VRage.Core.Services.Data.ModMetadata",
            ["Id"] = new JsonObject { ["Id"] = ModId(id), ["ServiceName"] = "Steam" },
            ["ProjectID"] = new JsonObject {
                ["Guid"] = "bbbbbbbb-cccc-dddd-eeee-ffffffffffff",
                ["ModId"] = new JsonObject { ["Id"] = ModId(id), ["ServiceName"] = "Steam" },
                ["ServiceId"] = "Steam"
            },
            ["Version"] = "from-working",
            ["BaseMetadata"] = new JsonObject {
                ["Id"] = null,
                ["Title"] = "Other mod title",
                ["Description"] = desc,
                ["Tags"] = new JsonArray("ModTagParticles", "UGCModTypeTag"),
                ["PreviewFilePath"] = "working-thumb.jpg"
            }
        },
        ["Author"] = author,
        ["LastLocallyUpdated"] = "2001-01-01T00:00:00Z"
    }
};
string project = Project(Target).ToJsonString();
string local = Local().ToJsonString();
string working = Template(Comparison, Owner).ToJsonString();

Expect(UniversalRecovery.Diagnose("", Target).Condition == MetadataCondition.Missing, "empty metadata detected");
Expect(UniversalRecovery.Diagnose(working, Comparison).Condition == MetadataCondition.Healthy, "healthy metadata detected");
Expect(UniversalRecovery.Diagnose(working[..^5], Comparison).Condition == MetadataCondition.Malformed, "truncated JSON detected");
Expect(UniversalRecovery.Diagnose(working, Target).Condition == MetadataCondition.IdentityConflict, "foreign item not treated as target");
Expect(UniversalRecovery.Diagnose(Template(Target, Owner, new string('x', 6000)).ToJsonString(), Target).Condition == MetadataCondition.OversizedDescription, "oversized description identified");
var oversizedOther = Template(Target, Owner);
oversizedOther["$Value"]!["Padding"] = new string('x', 6000);
Expect(UniversalRecovery.Diagnose(oversizedOther.ToJsonString(), Target).Condition == MetadataCondition.OversizedOther, "other oversized payload not blindly patched");

var plan = UniversalRecovery.FromWorkingItem(Target, Owner, Owner, Owner, "", project, local, working);
var candidate = JsonNode.Parse(plan.CandidateJson)!;
var value = candidate["$Value"]!;
Expect(plan.EligibleForApply && plan.Source == RecoverySource.WorkingItem && plan.CandidateBytes < 5000, "working comparison produces bounded candidate");
Expect(value["Id"]!.GetValue<ulong>() == Target && value["Author"]!.GetValue<ulong>() == Owner, "target ID and author preserved");
Expect(value["FullMetadata"]!["ProjectID"]!["Guid"]!.GetValue<string>() == "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "original project GUID retained");
Expect(value["FullMetadata"]!["Id"]!["Id"]!.GetValue<string>() == ModId(Target), "original Steam ModId preserved");
var basic = value["FullMetadata"]!["BaseMetadata"]!;
Expect(basic["Title"]!.GetValue<string>() == "Original title" && basic["Description"]!.GetValue<string>() == "", "original title; empty internal description");
Expect(basic["Tags"]!.AsArray().All(t => t!.GetValue<string>() != "ModTagParticles"), "comparison tags not leaked");
Expect(basic["PreviewFilePath"] is null, "comparison preview not leaked");
Expect(basic["WorkshopURL"] is null, "working-item Workshop URL not inherited");
Expect(basic["Dependencies"] is null && basic["OwnerName"] is null, "working-item dependencies/owner not inherited");
var withForeignUrl = Template(Comparison, Owner);
withForeignUrl["$Value"]!["FullMetadata"]!["BaseMetadata"]!["WorkshopURL"] = "https://steamcommunity.com/sharedfiles/filedetails/?id=3820098766";
var noForeignUrl = UniversalRecovery.FromWorkingItem(Target, Owner, Owner, Owner, "", project, local, withForeignUrl.ToJsonString());
Expect(JsonNode.Parse(noForeignUrl.CandidateJson)!["$Value"]!["FullMetadata"]!["BaseMetadata"]!["WorkshopURL"] is null,
    "explicit foreign URL sanitized in candidate");
var unknownTemplate = Template(Comparison, Owner);
unknownTemplate["$Value"]!["FullMetadata"]!["BaseMetadata"]!["UnknownPrivateField"] = "other mod secret";
Refuse(() => UniversalRecovery.FromWorkingItem(Target, Owner, Owner, Owner, "", project, local, unknownTemplate.ToJsonString()),
    "unknown template fields rejected instead of blindly copied");
Refuse(() => UniversalRecovery.FromWorkingItem(Target, Owner, Owner + 1, Owner, "", project, local, working), "wrong target owner rejects");
Refuse(() => UniversalRecovery.FromWorkingItem(Target, Owner, Owner, Owner + 1, "", project, local, working), "wrong comparison owner rejects");
Refuse(() => UniversalRecovery.FromWorkingItem(Target, Owner, Owner, Owner, working, project, local, working), "healthy current item cannot be overwritten");
Refuse(() => UniversalRecovery.FromWorkingItem(Target, Owner, Owner, Owner, "", Project(Comparison).ToJsonString(), local, working), "wrong local project binding rejects");
Refuse(() => UniversalRecovery.FromWorkingItem(Target, Owner, Owner, Owner, "", project, local, Template(Comparison, Owner+1).ToJsonString()), "wrong template author rejects");

var noLocal = UniversalRecovery.FromWorkingItem(Target, Owner, Owner, Owner, "", project, null, working);
Expect(JsonNode.Parse(noLocal.CandidateJson)!["$Value"]!["FullMetadata"]!["BaseMetadata"]!["Title"]!.GetValue<string>() == "Original Mod", "title falls back to original project");
var generic = UniversalRecovery.FromGenericReference(Target, project, local, Owner, Owner);
Expect(!generic.EligibleForApply && generic.Source == RecoverySource.GenericReference && generic.CandidateBytes < 5000, "generic reference previews without write authorization");
var originalBackup = Template(Target, Owner).ToJsonString();
Expect(UniversalRecovery.FromOriginalBackup(Target, Owner, Owner, "", originalBackup).EligibleForApply, "complete original backup supported");
Refuse(() => UniversalRecovery.FromOriginalBackup(Target, Owner, Owner, "", working), "backup for other ID rejects");
Refuse(() => UniversalRecovery.FromOriginalBackup(Target, Owner, Owner, "", Template(Target, Owner, new string('x', 6000)).ToJsonString()), "oversized backup rejects");

// End-to-end READ-ONLY session tests with a fake fresh Steam response.
// A failed query must NEVER look like a successful zero-byte result.
RecoveryRemoteSnapshot Snapshot(ulong id, ulong owner, string raw,
    bool succeeded = true, bool cached = false, uint appId = 1133870) =>
    new(id, appId, owner, succeeded, cached, raw);

int reads = 0;
RecoveryRemoteSnapshot Fresh(ulong id) {
    reads++;
    return id == Target ? Snapshot(id, Owner, "") :
        id == Comparison ? Snapshot(id, Owner, working) :
        throw new Exception("Unexpected fake Workshop item query");
}
var recoveredWithWorking = RecoveryReadOnlySession.Prepare(
    Target, Owner, Fresh, null, project, local, Comparison);
Expect(recoveredWithWorking.Condition == MetadataCondition.Missing &&
       recoveredWithWorking.Plan.Source == RecoverySource.WorkingItem &&
       recoveredWithWorking.ReferenceSha256 is not null,
       "fresh 0-byte + original local project + working reference produces read-only proposal");
Expect(reads == 4, "both original and reference re-read uncached before preview completion");
reads = 0;
var recoveredFromBackup = RecoveryReadOnlySession.Prepare(
    Target, Owner, Fresh, originalBackup, null, null, null);
Expect(recoveredFromBackup.Plan.Source == RecoverySource.OriginalBackup && reads == 2,
    "exact original backup proposal checks target before and after");
reads = 0;
var fallbackNoTemplate = RecoveryReadOnlySession.Prepare(
    Target, Owner, Fresh, null, project, local, null);
Expect(fallbackNoTemplate.Plan.Source == RecoverySource.GenericReference &&
       !fallbackNoTemplate.Plan.EligibleForApply && reads == 2,
       "no comparison mod produces a NON-WRITABLE generic proposal");

var malformedRemote = RecoveryReadOnlySession.Prepare(
    Target, Owner, id => Snapshot(id, Owner, "{truncated"), originalBackup,
    null, null, null);
Expect(malformedRemote.Condition == MetadataCondition.Malformed &&
       malformedRemote.Plan.Source == RecoverySource.OriginalBackup,
       "malformed/truncated remote metadata can use exact same-item backup, read-only");
RefuseUnsafe(() => RecoveryReadOnlySession.Prepare(Target, Owner,
    id => Snapshot(id, Owner, working), originalBackup, null, null, null),
    "foreign/identity-conflict JSON can NEVER be overwritten as malformed");

RefuseUnsafe(() => RecoveryReadOnlySession.Prepare(Target, Owner,
    id => Snapshot(id, Owner, "", succeeded: false), null, project, local, null),
    "Steam query failure NEVER misclassified as zero-byte metadata");
RefuseUnsafe(() => RecoveryReadOnlySession.Prepare(Target, Owner,
    id => Snapshot(id, Owner, "", cached: true), null, project, local, null),
    "cached Steam query blocks recovery");
RefuseUnsafe(() => RecoveryReadOnlySession.Prepare(Target, Owner,
    id => Snapshot(id, Owner, "", appId: 0), null, project, local, null),
    "wrong Steam AppId blocks recovery");
RefuseUnsafe(() => RecoveryReadOnlySession.Prepare(Target, Owner,
    id => Snapshot(id + 1, Owner, ""), null, project, local, null),
    "wrong Workshop identity blocks recovery");
RefuseUnsafe(() => RecoveryReadOnlySession.Prepare(Target, Owner,
    id => Snapshot(id, Owner + 1, ""), null, project, local, null),
    "wrong owner blocks recovery");
RefuseUnsafe(() => RecoveryReadOnlySession.Prepare(Target, Owner,
    id => new RecoveryRemoteSnapshot(id, 1133870, Owner, true, false, null),
    null, project, local, null),
    "null Steam metadata cannot masquerade as confirmed empty");
RefuseUnsafe(() => RecoveryReadOnlySession.Prepare(Target, Owner,
    id => Snapshot(id, Owner, originalBackup), null, project, local, null),
    "healthy remote metadata refuses reconstruction");
int staleOriginalReads = 0;
RefuseUnsafe(() => RecoveryReadOnlySession.Prepare(Target, Owner,
    id => Snapshot(id, Owner, staleOriginalReads++ == 0 ? "" : "{changed}"),
    originalBackup, null, null, null),
    "original changes between read and preview completion: STOP");
int staleReferenceReads = 0;
RefuseUnsafe(() => RecoveryReadOnlySession.Prepare(Target, Owner,
    id => id == Target ? Snapshot(Target, Owner, "") :
      Snapshot(Comparison, Owner, staleReferenceReads++ == 0 ? working : Template(Comparison, Owner, "changed").ToJsonString()),
    null, project, local, Comparison),
    "reference changes between two queries: STOP");
RefuseUnsafe(() => RecoveryReadOnlySession.Prepare(Target, Owner,
    id => id == Target ? Snapshot(Target, Owner, "") :
      Snapshot(Comparison, Owner + 1, working),
    null, project, local, Comparison),
    "different working-item owner blocks preview");
RefuseUnsafe(() => RecoveryReadOnlySession.Prepare(Target, Owner,
    Fresh, null, null, null, null),
    "no original project, backup or reference cannot generate recovery identity");
RefuseUnsafe(() => RecoveryReadOnlySession.Prepare(Target, Owner,
    Fresh, originalBackup, null, null, Comparison),
    "conflicting backup and comparison source selection rejected");


// Offline future-APPLY authorization: nothing below has a Steam write path.
// Accept ONLY old exact-item backups or owner-verified comparison proposals.
var authorizationFromBackup = RecoveryWritePreflight.Authorize(
    recoveredFromBackup, Owner, Fresh,
    recoveredFromBackup.OriginalRaw, recoveredFromBackup.Plan.CandidateJson, null,
    "RESTORE " + Target);
Expect(authorizationFromBackup.ItemId == Target &&
       authorizationFromBackup.Source == RecoverySource.OriginalBackup &&
       authorizationFromBackup.CandidateSha256 == recoveredFromBackup.CandidateSha256,
       "exact original backup passes offline-only write preflight after new live re-query");
var authorizationFromComparison = RecoveryWritePreflight.Authorize(
    recoveredWithWorking, Owner, Fresh,
    recoveredWithWorking.OriginalRaw, recoveredWithWorking.Plan.CandidateJson,
    recoveredWithWorking.ReferenceRaw, "RESTORE " + Target);
Expect(authorizationFromComparison.Source == RecoverySource.WorkingItem &&
       authorizationFromComparison.ReferenceSha256 == recoveredWithWorking.ReferenceSha256,
       "working reference requires and passes independent last-minute reference re-query");
RefuseUnsafe(() => RecoveryWritePreflight.Authorize(
    fallbackNoTemplate, Owner, Fresh,
    fallbackNoTemplate.OriginalRaw, fallbackNoTemplate.Plan.CandidateJson, null,
    "RESTORE " + Target),
    "generic fallback can NEVER authorize a Steam upload even with confirmation");
RefuseUnsafe(() => RecoveryWritePreflight.Authorize(
    recoveredFromBackup, Owner, Fresh,
    recoveredFromBackup.OriginalRaw, recoveredFromBackup.Plan.CandidateJson, null,
    "RESTORE " + Comparison),
    "wrong Workshop ID in typed approval blocks authorization");
RefuseUnsafe(() => RecoveryWritePreflight.Authorize(
    recoveredFromBackup, Owner + 1, Fresh,
    recoveredFromBackup.OriginalRaw, recoveredFromBackup.Plan.CandidateJson, null,
    "RESTORE " + Target),
    "wrong current signed-in owner blocks authorization");
RefuseUnsafe(() => RecoveryWritePreflight.Authorize(
    recoveredFromBackup, Owner, Fresh,
    "missing-original-backup", recoveredFromBackup.Plan.CandidateJson, null,
    "RESTORE " + Target),
    "modified original backup blocks authorization");
RefuseUnsafe(() => RecoveryWritePreflight.Authorize(
    recoveredFromBackup, Owner, Fresh,
    recoveredFromBackup.OriginalRaw, "{}", null,
    "RESTORE " + Target),
    "modified candidate backup blocks authorization");
RefuseUnsafe(() => RecoveryWritePreflight.Authorize(
    recoveredWithWorking, Owner, Fresh,
    recoveredWithWorking.OriginalRaw, recoveredWithWorking.Plan.CandidateJson,
    null, "RESTORE " + Target),
    "missing private reference backup blocks authorization");
RefuseUnsafe(() => RecoveryWritePreflight.Authorize(
    recoveredWithWorking, Owner, Fresh,
    recoveredWithWorking.OriginalRaw, recoveredWithWorking.Plan.CandidateJson,
    "modified-reference", "RESTORE " + Target),
    "tampered private reference backup blocks authorization");
RefuseUnsafe(() => RecoveryWritePreflight.Authorize(
    recoveredWithWorking, Owner,
    id => id == Target ? Snapshot(Target, Owner, "") :
      Snapshot(Comparison, Owner, Template(Comparison, Owner, "changed").ToJsonString()),
    recoveredWithWorking.OriginalRaw, recoveredWithWorking.Plan.CandidateJson,
    recoveredWithWorking.ReferenceRaw, "RESTORE " + Target),
    "stale reference metadata blocks last-minute authorization");
RefuseUnsafe(() => RecoveryWritePreflight.Authorize(
    recoveredFromBackup, Owner,
    id => Snapshot(Target, Owner, "{changed}"),
    recoveredFromBackup.OriginalRaw, recoveredFromBackup.Plan.CandidateJson,
    null, "RESTORE " + Target),
    "changed target metadata blocks last-minute authorization");
RefuseUnsafe(() => RecoveryWritePreflight.Authorize(
    recoveredFromBackup, Owner,
    id => Snapshot(Target, Owner, "", succeeded: false),
    recoveredFromBackup.OriginalRaw, recoveredFromBackup.Plan.CandidateJson,
    null, "RESTORE " + Target),
    "failed final Steam query blocks authorization");
RefuseUnsafe(() => RecoveryWritePreflight.Authorize(
    recoveredFromBackup, Owner,
    id => Snapshot(Target, Owner, "", cached: true),
    recoveredFromBackup.OriginalRaw, recoveredFromBackup.Plan.CandidateJson,
    null, "RESTORE " + Target),
    "cached final Steam query blocks authorization");
RefuseUnsafe(() => RecoveryWritePreflight.Authorize(
    recoveredFromBackup, Owner,
    id => Snapshot(Target, Owner + 1, ""),
    recoveredFromBackup.OriginalRaw, recoveredFromBackup.Plan.CandidateJson,
    null, "RESTORE " + Target),
    "changed live owner blocks authorization");


// Steamworks metadata API probe: FALSE is NOT the same as TRUE + empty.
SteamProbeObservation Evidence(bool detailsOk, bool metaOk, string? raw,
    bool cached = false, uint appId = 1133870,
    ulong item = Target, ulong owner = Owner) =>
    new(Target, item, appId, owner, Owner, detailsOk, cached, metaOk, raw);
var realZero = SteamMetadataProbePolicy.Evaluate(Evidence(true, true, ""));
Expect(realZero.Status == SteamProbeStatus.ConfirmedEmpty &&
       realZero.EligibleForRecoveryPreview && realZero.MetadataBytes == 0,
       "read-only probe distinguishes TRUE + zero bytes");
var noMetadataCall = SteamMetadataProbePolicy.Evaluate(Evidence(true, false, ""));
Expect(noMetadataCall.Status == SteamProbeStatus.MetadataCallFailed &&
       !noMetadataCall.EligibleForRecoveryPreview && noMetadataCall.MetadataBytes is null,
       "read-only probe FALSE with blank output is UNKNOWN, never missing");
var failedDetails = SteamMetadataProbePolicy.Evaluate(Evidence(false, false, ""));
Expect(failedDetails.Status == SteamProbeStatus.QueryFailed,
       "read-only probe fails closed on unsuccessful details query");
Expect(SteamMetadataProbePolicy.Evaluate(Evidence(true, true, "test")).Status == SteamProbeStatus.Present,
       "read-only probe reports metadata present when returned");
Expect(SteamMetadataProbePolicy.Evaluate(Evidence(true, true, null)).Status == SteamProbeStatus.AmbiguousNull,
       "read-only probe refuses success plus null output");
Expect(SteamMetadataProbePolicy.Evaluate(Evidence(true, true, "", cached: true)).Status == SteamProbeStatus.CachedQuery,
       "read-only probe refuses cached empty data");
Expect(SteamMetadataProbePolicy.Evaluate(Evidence(true, true, "", appId: 0)).Status == SteamProbeStatus.UnexpectedItem,
       "read-only probe refuses wrong app");
Expect(SteamMetadataProbePolicy.Evaluate(Evidence(true, true, "", item: Comparison)).Status == SteamProbeStatus.UnexpectedItem,
       "read-only probe refuses wrong Workshop item");
Expect(SteamMetadataProbePolicy.Evaluate(Evidence(true, true, "", owner: Owner + 1)).Status == SteamProbeStatus.WrongOwner,
       "read-only probe refuses wrong Steam owner");


// Consent/one-attempt policy: does not access Steam and must fail closed.
Expect(TestFixturePolicy.MayCreate(TestFixturePolicy.Confirmation, false),
    "exact typed test-item confirmation accepted for first attempt");
Expect(!TestFixturePolicy.MayCreate("CREATE EMPTY SE2 TEST ITEM ", false),
    "test-item confirmation rejects extra whitespace");
Expect(!TestFixturePolicy.MayCreate("create empty se2 test item", false),
    "test-item confirmation is case sensitive");
Expect(!TestFixturePolicy.MayCreate("", false) &&
       !TestFixturePolicy.MayCreate(null, false),
    "blank fixture confirmations blocked");
Expect(!TestFixturePolicy.MayCreate(TestFixturePolicy.Confirmation, true),
    "existing fixture creation ledger blocks duplicate creation");


// FULL BATCH end-to-end simulation for one genuine-project-shaped disposable mod.
var sampleProject = Project(Target);
sampleProject["$Value"]!["Name"] = "SE2 UGC E2E TEST - Isolation";
var sampleMetadata = Template(Target, Owner);
sampleMetadata["$Value"]!["FullMetadata"]!["BaseMetadata"]!["Title"] =
    "SE2 UGC E2E TEST - Isolation";
// Real ModHUB publications usually contain a nonempty internal description.
// The earlier empty-only test fixture accidentally hid this classification bug.
sampleMetadata["$Value"]!["FullMetadata"]!["BaseMetadata"]!["Description"] =
    "Real published ModHUB description — do not copy this to the synthetic oversized-other fixture.";
sampleMetadata["$Value"]!["FullMetadata"]!["ProjectID"]!["Guid"] =
    "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
var manifest = DisposableModScenarios.Prepare(Target, Owner, Owner,
    sampleProject.ToJsonString(), sampleMetadata.ToJsonString());
Expect(manifest.Scenarios.Count == 5 &&
       manifest.Scenarios.Count(c => c.MayWriteToDisposableItem) == 3,
    "single real ModHUB-bound E2E fixture prepares all five failure cases");
var otherFault = manifest.Scenarios.Single(c => c.Name == "oversized-other-rejection");
var originalDescription = JsonNode.Parse(manifest.OriginalRaw)!["$Value"]!["FullMetadata"]!["BaseMetadata"]!["Description"]!.GetValue<string>();
var otherDescription = JsonNode.Parse(otherFault.Raw)!["$Value"]!["FullMetadata"]!["BaseMetadata"]!["Description"]!.GetValue<string>();
Expect(originalDescription.Length > 0 && otherDescription.Length == 0 &&
       UniversalRecovery.Diagnose(otherFault.Raw, Target).Condition == MetadataCondition.OversizedOther,
    "E2E oversized-other isolates a real nonempty ModHUB description without changing saved original");
var mockAll = new E2EMemorySteam(Target, Owner, manifest.OriginalRaw);
var batch = DisposableE2EEngine.Run(manifest, mockAll);
Expect(batch.Complete && batch.OriginalRestored &&
       mockAll.Current == manifest.OriginalRaw && mockAll.Submits == 6,
    "single E2E batch injects three live faults, restores after each, tests oversized offline");
Expect(batch.Steps.Count == 5 && batch.Steps.All(x => x.Status.StartsWith("PASS-")),
    "single batch returns one complete five-scenario report");
var mockRejectEmpty = new E2EMemorySteam(Target, Owner, manifest.OriginalRaw) {
    RejectZeroByteWrite = true
};
var partialBatch = DisposableE2EEngine.Run(manifest, mockRejectEmpty);
Expect(!partialBatch.Complete && partialBatch.OriginalRestored &&
       mockRejectEmpty.Current == manifest.OriginalRaw &&
       partialBatch.Steps.Any(x => x.Status == "BLOCKED"),
    "Steam refusing empty metadata does not damage baseline or claim a full pass");
RefuseUnsafe(() => DisposableModScenarios.Prepare(Target, Owner+1, Owner,
    sampleProject.ToJsonString(), sampleMetadata.ToJsonString()),
    "full ModHUB test refuses wrong signed-in owner");
var fakeProject = Project(Target);
RefuseUnsafe(() => DisposableModScenarios.Prepare(Target, Owner, Owner,
    fakeProject.ToJsonString(), sampleMetadata.ToJsonString()),
    "full ModHUB test refuses non-test project title");
RefuseUnsafe(() => DisposableModScenarios.Prepare(3810512193UL, Owner, Owner,
    sampleProject.ToJsonString(), sampleMetadata.ToJsonString()),
    "full ModHUB test refuses production x100 Workshop ID");
var fakeOtherProject = Project(Comparison);
fakeOtherProject["$Value"]!["Name"] = "SE2 UGC E2E TEST - Isolation";
RefuseUnsafe(() => DisposableModScenarios.Prepare(Target, Owner, Owner,
    fakeOtherProject.ToJsonString(), sampleMetadata.ToJsonString()),
    "full ModHUB test rejects different .vrgproj Workshop ID");


Console.WriteLine($"ALL {count} OFFLINE SAFETY CHECKS PASSED; NO STEAM API WAS CALLED.");

sealed class E2EMemorySteam : IDisposableUgcTransport
{
    private readonly ulong _id;
    private readonly ulong _owner;
    internal string Current;
    internal int Submits;
    internal bool RejectZeroByteWrite;
    internal E2EMemorySteam(ulong id, ulong owner, string raw) {
        _id = id; _owner = owner; Current = raw;
    }
    public DisposableRemote ReadFresh(ulong itemId) {
        if (itemId != _id) throw new InvalidOperationException("Wrong Workshop ID");
        return new DisposableRemote(Current, _owner);
    }
    public void SubmitMetadataOnly(ulong itemId, string raw) {
        if (itemId != _id) throw new InvalidOperationException("Wrong Workshop ID");
        if (RejectZeroByteWrite && raw.Length == 0)
            throw new InvalidOperationException("Steam metadata empty payload rejected");
        Current = raw;
        Submits++;
    }
}

