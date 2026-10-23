// Deliberately independent of Steamworks and WinForms, so the exact patch logic
// used for live updates can be tested without Steam or any Workshop permissions.
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SE2WorkshopUGCFix;

internal sealed record PatchEvaluation(
    bool Eligible, string Reason, string Title, int OriginalBytes,
    int? CandidateBytes, int? DescriptionChars, string? CandidateRaw);

internal static class MetadataPatch
{
    internal const int LimitBytes = 5000;
    private static readonly Regex DescriptionMatcher = new(
        "(\\\"Description\\\"\\s*:\\s*)(\\\"(?:\\\\.|[^\\\"\\\\])*\\\")",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));

    internal static PatchEvaluation Evaluate(string raw, ulong expectedWorkshopId)
    {
        int originalSize = Encoding.UTF8.GetByteCount(raw);
        string title = $"Workshop item {expectedWorkshopId}";
        int? descriptionLength = null;
        PatchEvaluation Reject(string reason) =>
            new(false, reason, title, originalSize, null, descriptionLength, null);

        try {
            using var doc = JsonDocument.Parse(raw);
            var value = doc.RootElement.GetProperty("$Value");
            var full = value.GetProperty("FullMetadata");
            var basic = full.GetProperty("BaseMetadata");
            title = basic.GetProperty("Title").GetString() ?? title;
            var description = basic.GetProperty("Description").GetString();
            descriptionLength = description?.Length;
            string? modIdA = full.GetProperty("Id").GetProperty("Id").GetString();
            string? modIdB = full.GetProperty("ProjectID").GetProperty("ModId").GetProperty("Id").GetString();
            string? guid = full.GetProperty("ProjectID").GetProperty("Guid").GetString();

            if (value.GetProperty("Id").GetUInt64() != expectedWorkshopId ||
                string.IsNullOrWhiteSpace(modIdA) || modIdA != modIdB ||
                !Guid.TryParse(guid, out var projectGuid) || projectGuid == Guid.Empty ||
                full.GetProperty("Id").GetProperty("ServiceName").GetString() != "Steam")
                return Reject("Workshop ID, Steam service or SE2 project identity does not match. No update permitted.");
            if (originalSize <= LimitBytes)
                return Reject($"Metadata is {originalSize} bytes, within the {LimitBytes}-byte limit. No fix is necessary.");
            if (description is null || description.Length < 100)
                return Reject("Oversized metadata has no long embedded ModHUB description. This fix does not apply.");

            // A textual, one-field edit preserves the exact original bytes for all
            // unrelated JSON fields. More than one Description is intentionally rejected.
            var matches = DescriptionMatcher.Matches(raw);
            if (matches.Count != 1)
                return Reject("Multiple Description fields (or none) found. Refusing an ambiguous change.");
            var target = matches[0].Groups[2];
            string candidate = raw[..target.Index] + "\"\"" + raw[(target.Index + target.Length)..];
            int candidateSize = Encoding.UTF8.GetByteCount(candidate);
            if (candidateSize >= LimitBytes)
                return Reject("Clearing the description does not reduce developer metadata below the safe size limit.");
            var expected = JsonNode.Parse(raw) ?? throw new JsonException("Missing JSON document");
            var edited = JsonNode.Parse(candidate) ?? throw new JsonException("Missing candidate JSON document");
            expected["$Value"]!["FullMetadata"]!["BaseMetadata"]!["Description"] = "";
            if (!JsonNode.DeepEquals(expected, edited))
                return Reject("Candidate would modify additional metadata fields. Update refused.");
            return new PatchEvaluation(true,
                $"Known oversized-description issue found. The internal {description.Length}-character description can be cleared without modifying any other JSON value. Public Workshop description remains separate.",
                title, originalSize, candidateSize, descriptionLength, candidate);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException
            or FormatException or OverflowException or RegexMatchTimeoutException) {
            return Reject($"Unknown or invalid SE2 developer metadata ({ex.GetType().Name}). Not safe to repair.");
        }
    }
}
