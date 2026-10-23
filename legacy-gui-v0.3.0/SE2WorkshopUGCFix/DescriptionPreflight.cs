// Read-only, offline estimate. Models a ModHUB/Steamworks .NET-style JSON
// serialization of BaseMetadata.Description; no Steam API calls here.
// Public Steam descriptions are stored separately and do not by themselves
// count towards the 5,000-byte developer metadata limit.
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SE2WorkshopUGCFix;

internal sealed record PreflightEstimate(
    int DescriptionChars, int DescriptionUtf8Bytes,
    int JsonMinBytes, int JsonConservativeBytes,
    bool UsesLiveTemplate, int BaseBytesMin, int BaseBytesConservative,
    int ProjectedMin, int ProjectedConservative, string TemplateNote)
{
    internal string RiskLabel => ProjectedConservative >= MetadataPatch.LimitBytes
        ? "DANGER: Would exceed 5,000 bytes if embedded in developer metadata."
        : ProjectedConservative >= 4500
            ? "WARNING: Close to the 5,000-byte limit if embedded."
            : "Below the limit in this model. NOT a guarantee that ModHUB will publish safely.";
}

internal static class DescriptionPreflight
{
    // Reference from one *repaired* SE2 x100 item; never present as a
    // measurement of the currently entered user's new/unscanned mod.
    internal const int ExampleReferenceSize = 1479;
    private static readonly JsonSerializerOptions Relaxed = new() {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private static readonly Regex DescriptionToken = new(
        "(\\\"Description\\\"\\s*:\\s*)(\\\"(?:\\\\.|[^\\\"\\\\])*\\\")",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));

    internal static PreflightEstimate Calculate(string description, string? scannedRemoteJson)
    {
        description ??= "";
        string minToken = JsonSerializer.Serialize(description, Relaxed);
        string conservativeToken = JsonSerializer.Serialize(description);
        int minTokenBytes = Encoding.UTF8.GetByteCount(minToken);
        int conservativeTokenBytes = Encoding.UTF8.GetByteCount(conservativeToken);
        bool usingTemplate = TryGetDescriptionToken(scannedRemoteJson, out int scannedTotal, out int oldTokenBytes);
        int baseBytes = usingTemplate ? scannedTotal - oldTokenBytes : ExampleReferenceSize - 2;
        string note = usingTemplate
            ? "Uses your last read-only Steam scan as a template. Assumes ModHUB embeds the text and uses comparable JSON serialization."
            : "NO LIVE TEMPLATE: uses a reference baseline from one repaired SE2 mod (~1,479 bytes). Actual size for YOUR mod may differ.";
        return new(
            description.Length, Encoding.UTF8.GetByteCount(description),
            minTokenBytes, conservativeTokenBytes, usingTemplate,
            baseBytes, baseBytes, checked(baseBytes + minTokenBytes),
            checked(baseBytes + conservativeTokenBytes), note);
    }

    private static bool TryGetDescriptionToken(string? raw, out int totalBytes, out int tokenBytes)
    {
        totalBytes = 0;
        tokenBytes = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        try {
            using var document = JsonDocument.Parse(raw);
            var basic = document.RootElement.GetProperty("$Value")
                .GetProperty("FullMetadata").GetProperty("BaseMetadata");
            if (basic.GetProperty("Description").ValueKind != JsonValueKind.String) return false;
            var matches = DescriptionToken.Matches(raw);
            // Conservative refusal of an ambiguous reference format.
            if (matches.Count != 1) return false;
            totalBytes = Encoding.UTF8.GetByteCount(raw);
            tokenBytes = Encoding.UTF8.GetByteCount(matches[0].Groups[2].Value);
            return totalBytes >= tokenBytes;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or RegexMatchTimeoutException) {
            return false;
        }
    }
}
