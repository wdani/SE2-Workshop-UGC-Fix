using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SE2WorkshopUGCFix;

const ulong WorkshopId = 3820098765;
int tests = 0;
void Check(bool success, string label)
{
    tests++;
    if (!success) throw new Exception("SAFETY TEST FAILED: " + label);
    Console.WriteLine("PASS: " + label);
}

string Fixture(string description)
{
    object value = new {
        Id = WorkshopId,
        FullMetadata = new {
            Id = new { Id = "00000000-0000-0000-0000-000000012345", ServiceName = "Steam" },
            ProjectID = new { Guid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", ModId = new { Id = "00000000-0000-0000-0000-000000012345" } },
            BaseMetadata = new { Title = "Synthetic fixture – NOT a real Steam item", Description = description }
        }
    };
    return JsonSerializer.Serialize(new Dictionary<string, object> { ["$Value"] = value });
}

string Change(string original, Action<JsonNode> edit)
{
    var json = JsonNode.Parse(original)!;
    edit(json);
    return json.ToJsonString();
}
string longDescription = new('d', 5500);
string valid = Fixture(longDescription);
var good = MetadataPatch.Evaluate(valid, WorkshopId);
Check(good.Eligible && good.CandidateRaw is not null, "Recognizes known oversized-description case");
// The assertion helper does not provide flow analysis for nullable values.
// Fail the test explicitly instead of suppressing the compiler warning.
string candidateRaw = good.CandidateRaw ?? throw new InvalidOperationException("Eligible evaluation returned no candidate metadata.");
Check(good.OriginalBytes > 5000 && good.CandidateBytes < 5000, "Reduces 5,000+ bytes below limit");
var expected = JsonNode.Parse(valid)!;
expected["$Value"]!["FullMetadata"]!["BaseMetadata"]!["Description"] = "";
Check(JsonNode.DeepEquals(expected, JsonNode.Parse(candidateRaw)), "Changes only internal description field");
Check(!MetadataPatch.Evaluate(candidateRaw, WorkshopId).Eligible, "Already fixed item is not offered a second repair");
Check(!MetadataPatch.Evaluate(Fixture("short"), WorkshopId).Eligible, "Normal metadata rejected");
Check(!MetadataPatch.Evaluate(valid, 12345).Eligible, "Wrong requested Workshop ID rejected");
Check(!MetadataPatch.Evaluate(Change(valid, node => node["$Value"]!["FullMetadata"]!["Id"]!["ServiceName"] = "Fake"), WorkshopId).Eligible, "Non-Steam service rejected");
Check(!MetadataPatch.Evaluate(Change(valid, node => node["$Value"]!["FullMetadata"]!["ProjectID"]!["ModId"]!["Id"] = "fake"), WorkshopId).Eligible, "Mismatched mod IDs rejected");
Check(!MetadataPatch.Evaluate(Change(valid, node => node["$Value"]!["FullMetadata"]!["ProjectID"]!["Guid"] = "bad-guid"), WorkshopId).Eligible, "Invalid project GUID rejected");
Check(!MetadataPatch.Evaluate(valid[..^1], WorkshopId).Eligible, "Truncated or invalid JSON rejected");
Check(!MetadataPatch.Evaluate(Change(valid, node => node["$Value"]!["FullMetadata"]!["Other"] = new JsonObject { ["Description"] = "another field" }), WorkshopId).Eligible, "Ambiguous description field rejected");
Check(!MetadataPatch.Evaluate(Change(valid, node => node["$Value"]!["FullMetadata"]!["Padding"] = new string('p', 5500)), WorkshopId).Eligible, "Metadata still oversized after candidate patch rejected");
Check(!MetadataPatch.Evaluate(Fixture(new string('x', 90)), WorkshopId).Eligible, "Short embedded description rejected");
Check(!MetadataPatch.Evaluate("{\"hello\":true}", WorkshopId).Eligible, "Unknown metadata schema rejected");
Check(MetadataPatch.Evaluate(Change(valid, node => node["$Value"]!["FullMetadata"]!["ExtraDescription"] = "unrelated"), WorkshopId).Eligible, "Unrelated uniquely named properties preserved");
// Preflight is deliberately a conditional estimate, NEVER a publish guarantee.
var noTemplate = DescriptionPreflight.Calculate("test", null);
Check(!noTemplate.UsesLiveTemplate && noTemplate.TemplateNote.Contains("REFERENCE", StringComparison.OrdinalIgnoreCase), "Preflight without scan labels approximate reference only");
Check(noTemplate.ProjectedMin > 0 && noTemplate.ProjectedConservative >= noTemplate.ProjectedMin, "Preflight estimates plausible bounds");
var shortPreflight = DescriptionPreflight.Calculate("Tiny text", Fixture(""));
Check(shortPreflight.UsesLiveTemplate && shortPreflight.ProjectedConservative < 5000, "Preflight uses live JSON template when available");
var longPreflight = DescriptionPreflight.Calculate(new string('a', 6000), Fixture(""));
Check(longPreflight.UsesLiveTemplate && longPreflight.ProjectedMin > 5000 && longPreflight.RiskLabel.StartsWith("DANGER"), "Preflight warns for text that would overflow developer metadata");
var unicodePreflight = DescriptionPreflight.Calculate("🚀🚀ä\n\"", Fixture(""));
Check(unicodePreflight.DescriptionUtf8Bytes > unicodePreflight.DescriptionChars && unicodePreflight.JsonConservativeBytes >= unicodePreflight.JsonMinBytes, "Preflight handles UTF-8 and JSON escape size");
var alreadyEmbedded = DescriptionPreflight.Calculate("Updated", Fixture(new string('z', 300)));
Check(alreadyEmbedded.UsesLiveTemplate && alreadyEmbedded.ProjectedConservative < 5000, "Preflight subtracts OLD description token before inserting new text");
var unknownTemplate = DescriptionPreflight.Calculate("x", "{invalid JSON");
Check(!unknownTemplate.UsesLiveTemplate, "Preflight refuses invalid template and labels reference estimate");
var ambiguousTemplate = DescriptionPreflight.Calculate("x", Change(Fixture(""), node => node["$Value"]!["FullMetadata"]!["Extra"] = new JsonObject { ["Description"] = "other" }));
Check(!ambiguousTemplate.UsesLiveTemplate, "Preflight refuses ambiguous multiple description fields");
Console.WriteLine($"ALL {tests} PURE C# SAFETY TESTS PASSED. No Steam access or Workshop modification occurred.");
