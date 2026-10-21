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

Console.WriteLine($"ALL {count} OFFLINE SAFETY CHECKS PASSED; NO STEAM API WAS CALLED.");
