// Pure, offline recovery planning. No Steam calls or uploads in this file.
// The GUI/Steam transport must independently verify fresh uncached details,
// the signed-in owner and the expected pre-submit SHA256 before any upload.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SE2WorkshopUGCFix;

internal enum MetadataCondition {
    Missing, Healthy, OversizedDescription, OversizedOther,
    Malformed, IdentityConflict
}
internal enum RecoverySource { OriginalBackup, WorkingItem, GenericReference }
internal sealed record MetadataDiagnosis(MetadataCondition Condition, int Bytes, string Explanation);
internal sealed record RecoveryPlan(
    bool EligibleForApply, RecoverySource Source, string Explanation,
    ulong ItemId, string ProjectGuid, int CandidateBytes, string CandidateJson);

internal static class UniversalRecovery
{
    internal const int SteamLimitBytes = 5000;

    internal static MetadataDiagnosis Diagnose(string? raw, ulong expectedItemId)
    {
        if (string.IsNullOrEmpty(raw))
            return new(MetadataCondition.Missing, 0,
                "Steam returned an empty developer-metadata string; this is not a deleted Workshop item.");
        int bytes = Encoding.UTF8.GetByteCount(raw);
        try {
            var root = JsonNode.Parse(raw) ?? throw new JsonException("Empty root");
            ValidateEnvelope(root, expectedItemId);
            if (bytes <= SteamLimitBytes)
                return new(MetadataCondition.Healthy, bytes, "Valid identity and metadata within Steam limit.");
            var description = root["$Value"]?["FullMetadata"]?["BaseMetadata"]?["Description"]?.GetValue<string>();
            return !string.IsNullOrWhiteSpace(description)
                ? new(MetadataCondition.OversizedDescription, bytes,
                    "Oversized JSON with embedded description. Run the existing one-field patch validator.")
                : new(MetadataCondition.OversizedOther, bytes,
                    "Oversized JSON not explained by an embedded description. No automatic rewrite.");
        }
        catch (JsonException) {
            return new(MetadataCondition.Malformed, bytes,
                "Invalid/truncated JSON. Save raw bytes and use verified recovery sources only.");
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or KeyNotFoundException
                                  or FormatException) {
            return new(MetadataCondition.IdentityConflict, bytes,
                "Metadata structure or Workshop identity is inconsistent. Refuse automatic changes.");
        }
    }

    // Restores a previously saved *complete* raw developer metadata blob for this exact item.
    // This is a separate recovery path, NOT the same as the oversized description patch.
    internal static RecoveryPlan FromOriginalBackup(
        ulong itemId, ulong signedInSteamId, ulong targetOwner,
        string currentRaw, string backupRaw)
    {
        CheckOwner(signedInSteamId, targetOwner);
        RequireRecoveryState(currentRaw, itemId);
        var root = JsonNode.Parse(backupRaw) ?? throw new ArgumentException("Invalid backup JSON");
        ValidateEnvelope(root, itemId);
        if (root["$Value"]?["Author"]?.GetValue<ulong>() != signedInSteamId)
            throw new ArgumentException("Original backup author does not match Workshop owner.");
        string candidate = backupRaw;
        int bytes = Encoding.UTF8.GetByteCount(candidate);
        if (bytes >= SteamLimitBytes)
            throw new ArgumentException("Backup exceeds safe Steam metadata size; use description patch first.");
        return new(true, RecoverySource.OriginalBackup,
            "Exact-item backup, valid author and identity. Live recheck and explicit approval still required.",
            itemId, ReadProjectGuid(root), bytes, candidate);
    }

    // The comparison item provides SCHEMA ONLY: its identity, title, version,
    // tags and description are never copied to the original item.
    internal static RecoveryPlan FromWorkingItem(
        ulong itemId, ulong signedInSteamId, ulong targetOwner, ulong comparisonOwner,
        string currentRaw, string originalVrgproj, string? originalModhubMetadata,
        string workingMetadataJson, string? manualTitle = null)
    {
        CheckOwner(signedInSteamId, targetOwner);
        CheckOwner(signedInSteamId, comparisonOwner);
        RequireRecoveryState(currentRaw, itemId);
        var project = ReadOriginalProject(originalVrgproj, itemId);
        var template = JsonNode.Parse(workingMetadataJson) ?? throw new ArgumentException("Invalid template");
        ulong workingId = template["$Value"]?["Id"]?.GetValue<ulong>()
            ?? throw new ArgumentException("Missing working Workshop ID");
        if (workingId == itemId)
            throw new ArgumentException("Comparison item must be different from the damaged item.");
        ValidateEnvelope(template, workingId);
        if (template["$Value"]?["Author"]?.GetValue<ulong>() != signedInSteamId)
            throw new ArgumentException("Working metadata author differs from verified Steam owner.");
        var local = ReadLocalMetadata(originalModhubMetadata, project);
        string title = PreferTitle(manualTitle, local?.Name, project.Name);
        if (!(template["$Bundles"]?["VRage"]?.GetValue<string>() ?? "").StartsWith("2.4.", StringComparison.Ordinal))
            throw new ArgumentException("Working-item schema version has not been reviewed; needs a compatibility update.");
        var working = template["$Value"]!;
        var full = working["FullMetadata"]!;
        working["Id"] = itemId;
        working["Author"] = signedInSteamId;
        working["IsReferenceToRemote"] = false;
        full["Id"] = new JsonObject { ["Id"] = project.ModId, ["ServiceName"] = "Steam" };
        full["ProjectID"] = new JsonObject {
            ["Guid"] = project.Guid,
            ["ModId"] = new JsonObject { ["Id"] = project.ModId, ["ServiceName"] = "Steam" },
            ["ServiceId"] = "Steam"
        };
        full["Version"] = local?.Version;
        var basic = full["BaseMetadata"] ?? throw new ArgumentException("Working template missing BaseMetadata");
        basic["Title"] = title;
        basic["Description"] = ""; // Internal only. Public Workshop description is untouched.
        basic["Tags"] = TagsNode(local?.Tags);
        basic["Id"] = null;
        basic["PreviewFilePath"] = null;
        basic["Dependencies"] = null;
        basic["OwnerName"] = null;
        basic["VotesUp"] = 0;
        basic["VotesDown"] = 0;
        basic["NumSubscriptions"] = 0;
        basic["Rating"] = 0;
        basic["IsSubscribed"] = false;
        basic["IsOwner"] = false;
        basic["Size"] = 0; // Never claim another mod's thumbnail.
        working["PreviewUrl"] = null;
        working["LastLocallyUpdated"] = DateTimeOffset.UtcNow.ToString("O");
        string candidate = template.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        ValidateEnvelope(JsonNode.Parse(candidate)!, itemId);
        int bytes = Encoding.UTF8.GetByteCount(candidate);
        if (bytes >= SteamLimitBytes) throw new ArgumentException("Candidate does not fit Steam limit.");
        return new(true, RecoverySource.WorkingItem,
            "Reconstructed from original local identity and owner-verified working-item schema, NOT the historical original. Live owner/template SHA256 recheck and explicit approval required.",
            itemId, project.Guid, bytes, candidate);
    }

    // For creators without an older backup or a separate working item.
    // Never upload this generic candidate until a compatible SE2/ModHUB schema
    // and the exact original identity have been independently validated.
    internal static RecoveryPlan FromGenericReference(
        ulong itemId, string originalVrgproj, string? originalModhubMetadata,
        ulong signedInSteamId, ulong targetOwner, string? manualTitle = null)
    {
        CheckOwner(signedInSteamId, targetOwner);
        var project = ReadOriginalProject(originalVrgproj, itemId);
        var local = ReadLocalMetadata(originalModhubMetadata, project);
        string title = PreferTitle(manualTitle, local?.Name, project.Name);
        var tags = TagsNode(local?.Tags);
        var root = new JsonObject {
            ["$Bundles"] = new JsonObject {
                ["System.Runtime"] = "1.0.0.0", ["VRage"] = "2.4.0.95"
            },
            ["$Type"] = "VRage:Keen.VRage.Steam.Data.SteamUgcMetadata",
            ["$Value"] = new JsonObject {
                ["Id"] = itemId,
                ["PreviewUrl"] = null,
                ["FullMetadata"] = new JsonObject {
                    ["$Type"] = "VRage:Keen.VRage.Core.Services.Data.ModMetadata",
                    ["Id"] = new JsonObject { ["Id"] = project.ModId, ["ServiceName"] = "Steam" },
                    ["ProjectID"] = new JsonObject {
                        ["Guid"] = project.Guid,
                        ["ModId"] = new JsonObject { ["Id"] = project.ModId, ["ServiceName"] = "Steam" },
                        ["ServiceId"] = "Steam"
                    },
                    ["Version"] = local?.Version,
                    ["BaseMetadata"] = new JsonObject {
                        ["Id"] = null, ["Title"] = title, ["Description"] = "",
                        ["Visiblity"] = "Public", ["Tags"] = tags,
                        ["IsSubscribed"] = false, ["VotesUp"] = 0, ["VotesDown"] = 0,
                        ["Rating"] = 0, ["IsOwner"] = false,
                        ["NumSubscriptions"] = 0, ["Size"] = 0, ["OwnerName"] = null,
                        ["CreatedAt"] = "0001-01-01T00:00:00.0000000",
                        ["UpdatedAt"] = "0001-01-01T00:00:00.0000000",
                        ["WorkshopURL"] = null, ["PreviewFilePath"] = null,
                        ["Dependencies"] = null
                    }
                },
                ["Author"] = signedInSteamId,
                ["IsReferenceToRemote"] = false,
                ["LastLocallyUpdated"] = DateTimeOffset.UtcNow.ToString("O")
            }
        };
        string candidate = root.ToJsonString();
        ValidateEnvelope(root, itemId);
        int bytes = Encoding.UTF8.GetByteCount(candidate);
        if (bytes >= SteamLimitBytes) throw new ArgumentException("Generic candidate oversized");
        return new(false, RecoverySource.GenericReference,
            "PREVIEW ONLY. Synthetic reference schema; compatibility with this SDK/version is UNVERIFIED. Must obtain a verified live template or developer review before any Steam change.",
            itemId, project.Guid, bytes, candidate);
    }

    private static void RequireRecoveryState(string currentRaw, ulong id)
    {
        var state = Diagnose(currentRaw, id).Condition;
        if (state is not (MetadataCondition.Missing or MetadataCondition.Malformed))
            throw new ArgumentException("Recovery only allowed on empty or malformed metadata; healthy records must never be replaced.");
    }

    private static void CheckOwner(ulong signedIn, ulong owner)
    {
        if (signedIn == 0 || owner != signedIn)
            throw new ArgumentException("Workshop owner does not match signed-in Steam user.");
    }

    private sealed record Project(string Guid, string ModId, string Name);
    private sealed record LocalInfo(string? Name, string? Version, string[]? Tags);

    private static Project ReadOriginalProject(string json, ulong workshopId)
    {
        var root = JsonNode.Parse(json) ?? throw new ArgumentException("Missing .vrgproj");
        if (root["$Type"]?.GetValue<string>() != "VRage:Keen.VRage.Core.Project.VRageProjectData")
            throw new ArgumentException("Not a VRage .vrgproj project");
        var value = root["$Value"] ?? throw new ArgumentException("Missing project root");
        var id = value["ProjectID"] ?? throw new ArgumentException("No ProjectID");
        var guid = id["Guid"]?.GetValue<string>();
        var modId = id["ModId"]?["Id"]?.GetValue<string>();
        if (!Guid.TryParse(guid, out var guidValue) || guidValue == Guid.Empty ||
            !Guid.TryParse(modId, out var modGuid) ||
            id["ModId"]?["ServiceName"]?.GetValue<string>() != "Steam" ||
            id["ServiceId"]?.GetValue<string>() != "Steam")
            throw new ArgumentException("Original project has no valid Steam identity and GUID.");
        var bytes = modGuid.ToByteArray();
        // Known SE2 Steam ModId representation embeds the uint32 Workshop ID
        // in GUID bytes 8..11. Refuse future unknown wider IDs instead of guessing.
        if (workshopId > uint.MaxValue || BitConverter.ToUInt32(bytes, 8) != workshopId ||
            bytes.Take(8).Any(b => b != 0) || bytes.Skip(12).Any(b => b != 0))
            throw new ArgumentException("Original .vrgproj ModId does not encode the requested Workshop ID.");
        return new(guidValue.ToString("D"), modGuid.ToString("D"),
            value["Name"]?.GetValue<string>() ?? "");
    }

    private static LocalInfo? ReadLocalMetadata(string? raw, Project project)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var root = JsonNode.Parse(raw) ?? throw new ArgumentException("Invalid local ModHUB file");
        if (root["$Type"]?.GetValue<string>() != "VRage:Keen.VRage.ModHUB.Models.ModProject")
            throw new ArgumentException("Not a ModHUB project metadata file");
        var val = root["$Value"] ?? throw new ArgumentException("Missing ModHUB project data");
        var stored = val["ProjectID"];
        var recordedGuid = stored?["Guid"]?.GetValue<string>();
        var recordedModId = stored?["ModId"]?["Id"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(recordedGuid) && recordedGuid != Guid.Empty.ToString("D") &&
            !string.Equals(recordedGuid, project.Guid, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Local ModHUB ProjectGUID conflicts with original .vrgproj.");
        if (!string.IsNullOrWhiteSpace(recordedModId) && recordedModId != Guid.Empty.ToString("D") &&
            !string.Equals(recordedModId, project.ModId, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Local ModHUB ModId conflicts with original .vrgproj.");
        var tags = val["Tags"] is JsonArray a ? a.Select(t => t?.GetValue<string>() ?? "").ToArray() : null;
        return new(val["Name"]?.GetValue<string>(), val["Version"]?.GetValue<string>(), tags);
    }

    private static JsonArray TagsNode(string[]? tags)
    {
        var safeTags = (tags ?? Array.Empty<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length <= 100).Distinct().ToList();
        if (!safeTags.Contains("UGCModTypeTag")) safeTags.Add("UGCModTypeTag");
        if (safeTags.Count > 30) throw new ArgumentException("Too many metadata tags");
        var result = new JsonArray();
        foreach (string tag in safeTags) result.Add(tag);
        return result;
    }

    private static string PreferTitle(params string?[] titles)
    {
        var title = titles.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        if (title is null || title.Length > 150) throw new ArgumentException("Valid original mod title is required.");
        return title.Trim();
    }

    private static void ValidateEnvelope(JsonNode root, ulong itemId)
    {
        var value = root["$Value"] ?? throw new ArgumentException("No $Value");
        if (root["$Type"]?.GetValue<string>() != "VRage:Keen.VRage.Steam.Data.SteamUgcMetadata" ||
            value["Id"]?.GetValue<ulong>() != itemId ||
            value["FullMetadata"]?["$Type"]?.GetValue<string>() != "VRage:Keen.VRage.Core.Services.Data.ModMetadata")
            throw new ArgumentException("Unexpected SE2 metadata envelope or Workshop ID");
        var full = value["FullMetadata"]!;
        var originalId = full["Id"]?["Id"]?.GetValue<string>();
        var nestedId = full["ProjectID"]?["ModId"]?["Id"]?.GetValue<string>();
        var projectGuid = full["ProjectID"]?["Guid"]?.GetValue<string>();
        if (!Guid.TryParse(originalId, out var modGuid) || originalId != nestedId ||
            !Guid.TryParse(projectGuid, out var guid) || guid == Guid.Empty ||
            full["Id"]?["ServiceName"]?.GetValue<string>() != "Steam" ||
            full["ProjectID"]?["ServiceId"]?.GetValue<string>() != "Steam" ||
            full["ProjectID"]?["ModId"]?["ServiceName"]?.GetValue<string>() != "Steam" ||
            itemId > uint.MaxValue || BitConverter.ToUInt32(modGuid.ToByteArray(), 8) != itemId ||
            modGuid.ToByteArray().Take(8).Any(b => b != 0) ||
            modGuid.ToByteArray().Skip(12).Any(b => b != 0))
            throw new ArgumentException("Mismatched or invalid project/Steam identity");
    }

    private static string ReadProjectGuid(JsonNode root) =>
        root["$Value"]!["FullMetadata"]!["ProjectID"]!["Guid"]!.GetValue<string>();
}
