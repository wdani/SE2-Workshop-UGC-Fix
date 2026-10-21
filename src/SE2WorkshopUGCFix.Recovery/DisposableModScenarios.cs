// Real-disposable-mod test fixture: validate actual ModHUB project identity and
// generate reproducible failure cases. Pure JSON logic, NO Steam requests.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace SE2WorkshopUGCFix;

internal sealed record DisposableScenario(string Name, string Raw, MetadataCondition Expected,
    bool MayWriteToDisposableItem);

internal sealed record DisposableTestManifest(
    ulong ItemId, ulong OwnerId, string ProjectGuid, string OriginalRaw,
    IReadOnlyList<DisposableScenario> Scenarios);

internal static class DisposableModScenarios
{
    internal const string RequiredTitlePrefix = "SE2 UGC E2E TEST";

    internal static DisposableTestManifest Prepare(
        ulong workshopId, ulong signedInSteamId, ulong actualOwner,
        string originalProjectVrgproj, string liveDeveloperMetadata)
    {
        if (workshopId == 0 || workshopId == 3810512193UL ||
            workshopId == 3509305158UL || signedInSteamId == 0 ||
            signedInSteamId != actualOwner)
            throw new ArgumentException("Dedicated disposable item and verified owner required.");
        if (string.IsNullOrWhiteSpace(originalProjectVrgproj) ||
            Encoding.UTF8.GetByteCount(originalProjectVrgproj) > 131072)
            throw new ArgumentException("Original local ModHUB project required.");

        if (UniversalRecovery.Diagnose(liveDeveloperMetadata, workshopId).Condition !=
            MetadataCondition.Healthy)
            throw new ArgumentException("Test must start from valid published SE2 developer metadata.");

        var root = JsonNode.Parse(liveDeveloperMetadata)
            ?? throw new ArgumentException("Invalid live JSON");
        var value = root["$Value"] ?? throw new ArgumentException("Missing $Value");
        if (value["Author"]?.GetValue<ulong>() != signedInSteamId)
            throw new ArgumentException("Steam metadata author is not the signed-in owner.");
        var full = value["FullMetadata"] ?? throw new ArgumentException("Missing full metadata");
        var guid = full["ProjectID"]?["Guid"]?.GetValue<string>()
            ?? throw new ArgumentException("Missing true SE2 ProjectGUID");
        var title = full["BaseMetadata"]?["Title"]?.GetValue<string>() ?? "";
        if (!title.StartsWith(RequiredTitlePrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only dedicated E2E TEST mods can be modified.");

        var project = JsonNode.Parse(originalProjectVrgproj)
            ?? throw new ArgumentException("Invalid original .vrgproj");
        if (project["$Type"]?.GetValue<string>() !=
            "VRage:Keen.VRage.Core.Project.VRageProjectData")
            throw new ArgumentException("Not a ModHUB/VRAGE3 project.");
        var pv = project["$Value"] ?? throw new ArgumentException("Missing project data");
        var projectId = pv["ProjectID"] ?? throw new ArgumentException("Missing project identity");
        if (!Guid.TryParse(guid, out var g) || g == Guid.Empty ||
            !string.Equals(projectId["Guid"]?.GetValue<string>(), guid,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(pv["Name"]?.GetValue<string>(), title,
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Actual ModHUB project GUID/name mismatch.");
        var remoteModId = full["Id"]?["Id"]?.GetValue<string>() ?? "";
        var originalModId = projectId["ModId"]?["Id"]?.GetValue<string>() ?? "";
        if (!string.Equals(remoteModId, originalModId, StringComparison.OrdinalIgnoreCase) ||
            projectId["ModId"]?["ServiceName"]?.GetValue<string>() != "Steam" ||
            projectId["ServiceId"]?.GetValue<string>() != "Steam")
            throw new ArgumentException("Original .vrgproj is not linked to this Steam item.");

        // Test the exact baseline and two deliberate, reversible real-UGC fault
        // conditions. Oversize cases remain *offline* because Steam rejects them.
        var foreign = JsonNode.Parse(liveDeveloperMetadata)!;
        foreign["$Value"]!["Id"] = workshopId + 1;
        var hugeDescription = JsonNode.Parse(liveDeveloperMetadata)!;
        hugeDescription["$Value"]!["FullMetadata"]!["BaseMetadata"]!["Description"] =
            new string('X', 5200);
        var hugeOther = JsonNode.Parse(liveDeveloperMetadata)!;
        hugeOther["ExtraOversizedField"] = new string('X', 5200);
        var scenarios = new [] {
            new DisposableScenario("malformed-json", "{", MetadataCondition.Malformed, true),
            new DisposableScenario("wrong-workshop-identity", foreign.ToJsonString(),
                MetadataCondition.IdentityConflict, true),
            new DisposableScenario("empty-metadata", "", MetadataCondition.Missing, true),
            new DisposableScenario("oversized-description-rejection",
                hugeDescription.ToJsonString(), MetadataCondition.OversizedDescription, false),
            new DisposableScenario("oversized-other-rejection",
                hugeOther.ToJsonString(), MetadataCondition.OversizedOther, false)
        };
        foreach (var scenario in scenarios) {
            if (UniversalRecovery.Diagnose(scenario.Raw, workshopId).Condition != scenario.Expected)
                throw new InvalidOperationException("Unexpected scenario classification " + scenario.Name);
            if (scenario.MayWriteToDisposableItem &&
                Encoding.UTF8.GetByteCount(scenario.Raw) >= UniversalRecovery.SteamLimitBytes)
                throw new InvalidOperationException("A writeable scenario exceeds Steam's byte limit.");
            if (!scenario.MayWriteToDisposableItem &&
                Encoding.UTF8.GetByteCount(scenario.Raw) <= UniversalRecovery.SteamLimitBytes)
                throw new InvalidOperationException("Oversize test is not actually oversized.");
        }
        return new(workshopId, signedInSteamId, guid, liveDeveloperMetadata, scenarios);
    }
}
