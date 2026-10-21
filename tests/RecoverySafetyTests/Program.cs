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
Console.WriteLine($"ALL {count} OFFLINE SAFETY CHECKS PASSED; NO STEAM API WAS CALLED.");
