using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SE2WorkshopUGCFix.Wpf;

/// <summary>
/// Fail-closed boundary between private Workshop backups and clipboard exports.
/// Only known, summary-only scan/probe report schemas may be copied.
/// No Steam API calls or Workshop mutations occur here.
/// </summary>
internal static class SafeReportGuard
{
    internal const string ReportFileName = "report-SAFE-TO-SHARE.json";
    private const long MaximumReportBytes = 64 * 1024;

    private static readonly HashSet<string> ScanFields = new(StringComparer.Ordinal)
    {
        "appId", "workshopItem", "dateUtc", "originalBytes", "originalHash",
        "candidateBytes", "candidateHash", "descriptionChars", "ownerMatches",
        "eligible", "reason", "classification", "workshopDescriptionUpdated",
        "contentUpdated"
    };

    private static readonly HashSet<string> ProbeFields = new(StringComparer.Ordinal)
    {
        "diagnosticType", "dateUtc", "appIdExpected", "requestedWorkshopItem",
        "detailQueryCode", "detailsQuerySucceeded", "responseWasCached",
        "returnedItemMatches", "consumerAppMatches", "ownedBySignedInUser",
        "getQueryUGCMetadataReturnedTrue", "rawMetadataOutputWasNull",
        "metadataBytes", "metadataSha256", "classification", "explanation",
        "steamUpdatesSubmitted"
    };

    internal static string ReadValidated(string path)
    {
        if (!string.Equals(Path.GetFileName(path), ReportFileName,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Only report-SAFE-TO-SHARE.json may be copied.");

        var file = new FileInfo(path);
        if (!file.Exists)
            throw new FileNotFoundException(
                "The safe report is no longer available. Run a fresh check.", path);
        if (file.Length <= 0 || file.Length > MaximumReportBytes)
            throw new InvalidDataException("The safe report has an unexpected size.");

        var text = File.ReadAllText(path);
        ValidateForCopy(text);
        return text;
    }

    internal static void ValidateForCopy(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("The safe report must be a JSON object.");

            bool probe = root.TryGetProperty("diagnosticType", out var diagnosticType);
            var known = probe ? ProbeFields : ScanFields;
            if (probe)
            {
                if (diagnosticType.ValueKind != JsonValueKind.String ||
                    diagnosticType.GetString() != "STEAM_READ_ONLY_PROBE" ||
                    !IsExpectedApp(root, "appIdExpected") ||
                    !IsNonZeroNumber(root, "requestedWorkshopItem") ||
                    !IsFalse(root, "steamUpdatesSubmitted"))
                    throw new InvalidDataException("Unrecognized read-only probe report.");
            }
            else if (!IsExpectedApp(root, "appId") ||
                     !IsNonZeroNumber(root, "workshopItem") ||
                     !IsFalse(root, "contentUpdated") ||
                     !IsFalse(root, "workshopDescriptionUpdated"))
                throw new InvalidDataException("Unrecognized safe scan report.");

            // JsonElement.TryGetProperty picks the last duplicate. A copied
            // report must not conceal a different value in an earlier entry.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in root.EnumerateObject())
            {
                if (!seen.Add(field.Name))
                    throw new InvalidDataException(
                        "Duplicate report fields are not permitted.");
                if (!known.Contains(field.Name) ||
                    field.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object ||
                    (field.Value.ValueKind == JsonValueKind.String &&
                     field.Value.GetString() is { Length: > 4096 }))
                    throw new InvalidDataException(
                        "The file contains unexpected/private fields and was not copied.");
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The safe report is not valid JSON.", ex);
        }
    }

    private static bool IsExpectedApp(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetUInt32(out uint appId) && appId == 1133870;

    private static bool IsNonZeroNumber(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetUInt64(out ulong itemId) && itemId != 0;

    private static bool IsFalse(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.False;
}
