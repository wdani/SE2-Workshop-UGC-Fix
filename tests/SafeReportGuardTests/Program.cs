using System;
using System.IO;
using SE2WorkshopUGCFix.Wpf;

int count = 0;
void Accept(string name, string json)
{
    SafeReportGuard.ValidateForCopy(json);
    count++;
    Console.WriteLine("PASS " + name);
}
void Block(string name, string json)
{
    bool refused = false;
    try { SafeReportGuard.ValidateForCopy(json); }
    catch (InvalidDataException) { refused = true; }
    if (!refused) throw new InvalidOperationException("UNSAFE ACCEPT: " + name);
    count++;
    Console.WriteLine("PASS blocked " + name);
}
void BlockFile(string name, string path)
{
    bool refused = false;
    try { _ = SafeReportGuard.ReadValidated(path); }
    catch (InvalidDataException) { refused = true; }
    if (!refused) throw new InvalidOperationException("UNSAFE FILE ACCEPT: " + name);
    count++;
    Console.WriteLine("PASS blocked " + name);
}

const string scan = """
{"appId":1133870,"workshopItem":3824260687,"dateUtc":"2026-10-24T12:48:21Z","originalBytes":1611,"originalHash":"E3F3353B0275FDA4330AB00A3A904ABC7A16B00AA2BD702D69FC4B7D38AD0270","candidateBytes":null,"candidateHash":null,"descriptionChars":132,"ownerMatches":true,"eligible":false,"reason":"Metadata is healthy.","classification":"Healthy","workshopDescriptionUpdated":false,"contentUpdated":false}
""";
const string probe = """
{"diagnosticType":"STEAM_READ_ONLY_PROBE","dateUtc":"2026-10-24T12:48:21Z","appIdExpected":1133870,"requestedWorkshopItem":3824260687,"detailQueryCode":"k_EResultOK","detailsQuerySucceeded":true,"responseWasCached":false,"returnedItemMatches":true,"consumerAppMatches":true,"ownedBySignedInUser":true,"getQueryUGCMetadataReturnedTrue":true,"rawMetadataOutputWasNull":false,"metadataBytes":1611,"metadataSha256":"E3F3353B0275FDA4330AB00A3A904ABC7A16B00AA2BD702D69FC4B7D38AD0270","classification":"Present","explanation":"Metadata returned successfully.","steamUpdatesSubmitted":false}
""";

Accept("realistic scan summary", scan);
Accept("realistic API probe summary", probe);
Block("private metadata payload", scan.Replace("\"contentUpdated\":false", "\"originalRaw\":{\"password\":\"SECRET\"},\"contentUpdated\":false"));
Block("private candidate string", scan.Replace("\"contentUpdated\":false", "\"candidateRaw\":\"SECRET\",\"contentUpdated\":false"));
Block("unexpected field", scan.Replace("\"contentUpdated\":false", "\"privatePath\":\"C:/secret\",\"contentUpdated\":false"));
Block("duplicate owner field", scan.Replace("\"ownerMatches\":true", "\"ownerMatches\":false,\"ownerMatches\":true"));
Block("duplicate app field", scan.Replace("\"appId\":1133870", "\"appId\":1,\"appId\":1133870"));
Block("duplicate report type", probe.Replace("\"diagnosticType\":\"STEAM_READ_ONLY_PROBE\"", "\"diagnosticType\":\"FAKE\",\"diagnosticType\":\"STEAM_READ_ONLY_PROBE\""));
Block("wrong scan application", scan.Replace("\"appId\":1133870", "\"appId\":123"));
Block("wrong probe application", probe.Replace("\"appIdExpected\":1133870", "\"appIdExpected\":123"));
Block("zero workshop ID", scan.Replace("\"workshopItem\":3824260687", "\"workshopItem\":0"));
Block("zero probe workshop ID", probe.Replace("\"requestedWorkshopItem\":3824260687", "\"requestedWorkshopItem\":0"));
Block("scan contentUpdated flag true", scan.Replace("\"contentUpdated\":false", "\"contentUpdated\":true"));
Block("scan description updated true", scan.Replace("\"workshopDescriptionUpdated\":false", "\"workshopDescriptionUpdated\":true"));
Block("probe steamUpdatesSubmitted true", probe.Replace("\"steamUpdatesSubmitted\":false", "\"steamUpdatesSubmitted\":true"));
Block("probe diagnostic type unknown", probe.Replace("STEAM_READ_ONLY_PROBE", "UNKNOWN"));
Block("nested unexpected report data", probe.Replace("\"explanation\":\"Metadata returned successfully.\"", "\"explanation\":{\"private\":\"metadata\"}"));
Block("owner flag changed to a string", scan.Replace("\"ownerMatches\":true", "\"ownerMatches\":\"true\""));
Block("scan classification type changed", scan.Replace("\"classification\":\"Healthy\"", "\"classification\":42"));
Block("probe metadataBytes type changed", probe.Replace("\"metadataBytes\":1611", "\"metadataBytes\":\"1611\""));
Block("probe query-success type changed", probe.Replace("\"detailsQuerySucceeded\":true", "\"detailsQuerySucceeded\":{}"));
Block("scan original bytes type changed", scan.Replace("\"originalBytes\":1611", "\"originalBytes\":\"1611\""));
Block("array as report", "[]");
Block("malformed JSON", "{bad");
Block("too long string", scan.Replace("Metadata is healthy.", new string('X', 4097)));

string tempDir = Path.Combine(Path.GetTempPath(),
    "se2-safe-report-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDir);
try
{
    string report = Path.Combine(tempDir, SafeReportGuard.ReportFileName);
    File.WriteAllText(report, scan);
    if (SafeReportGuard.ReadValidated(report) != scan)
        throw new InvalidOperationException("Validated file roundtrip did not match.");
    count++; Console.WriteLine("PASS validated on-disk scan report");
    File.WriteAllText(report, probe);
    if (SafeReportGuard.ReadValidated(report) != probe)
        throw new InvalidOperationException("Validated probe roundtrip did not match.");
    count++; Console.WriteLine("PASS validated on-disk API probe report");
    string privateFile = Path.Combine(tempDir, "original-developer-metadata-PRIVATE.json");
    File.WriteAllText(privateFile, scan);
    BlockFile("private backup filename", privateFile);
    File.WriteAllText(report, scan + new string(' ', 70_000));
    BlockFile("oversized report file", report);
    File.WriteAllText(report, "");
    BlockFile("empty report file", report);
    File.Delete(report);
    bool missingRefused = false;
    try { _ = SafeReportGuard.ReadValidated(report); }
    catch (FileNotFoundException) { missingRefused = true; }
    if (!missingRefused) throw new InvalidOperationException("Missing report was accepted.");
    count++; Console.WriteLine("PASS blocked missing report file");
}
finally
{
    Directory.Delete(tempDir, recursive: true);
}
Console.WriteLine($"ALL {count} SAFE-REPORT TESTS PASSED. No Steam connection or metadata writes.");
