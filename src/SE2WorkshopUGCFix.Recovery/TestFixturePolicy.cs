using System;
using System.IO;

namespace SE2WorkshopUGCFix;

internal static class TestFixturePolicy
{
    internal const uint AppId = 1133870;
    internal const string Confirmation = "CREATE EMPTY SE2 TEST ITEM";

    internal static bool MayCreate(string? typed, bool alreadyAttempted) =>
        !alreadyAttempted && string.Equals(typed, Confirmation, StringComparison.Ordinal);

    internal static string LedgerDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SE2WorkshopUGCFix", "TestFixture");

    internal static string LedgerPath => Path.Combine(LedgerDirectory, "creation-attempt.json");

    internal static string ResultPath => Path.Combine(LedgerDirectory, "creation-result.json");
}
