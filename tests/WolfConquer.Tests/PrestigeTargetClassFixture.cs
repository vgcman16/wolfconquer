namespace WolfConquer.Tests;

// Numeric IDs only, independently transcribed from the current D13 primary
// profession resource. No display labels or client asset payloads are embedded.
internal static class PrestigeTargetClassFixture
{
    internal sealed record FamilyBand(uint First, uint Last, uint Category);

    internal static readonly FamilyBand[] Families =
    [
        new(1000, 1057, 1), new(2000, 2057, 2), new(4000, 4057, 3),
        new(5000, 5057, 4), new(6000, 6057, 5), new(7000, 7057, 6),
        new(8000, 8057, 7), new(13002, 13057, 8), new(14002, 14057, 9),
        new(16000, 16057, 10), new(9000, 9057, 11),
    ];

    internal static readonly uint[] ObservedUnmodeled =
    [
        11002, 11003, 11004, 11005, 12002, 12003, 12004, 12005,
        15002, 15003, 15004, 15005, 19000, 19001, 19002, 19003, 19004,
    ];

    // 10000/10001 occur in the target primary; 10002 is the retained exact
    // compatibility exclusion and is absent from that primary, not observed data.
    internal static readonly uint[] WorldOnly = [10000, 10001, 10002];

    internal static IEnumerable<object[]> FamilyEdges()
    {
        foreach (var family in Families)
        foreach (var id in new[] { family.First, family.Last - 6, family.Last - 5, family.Last })
            yield return [id, family.Category]; // First, legacy51, newly admitted52, target57.
    }

    internal static IEnumerable<object[]> ExtendedSuffixes()
    {
        foreach (var family in Families)
        foreach (var id in new[] { family.Last - 5, family.Last })
            yield return [id, family.Category];
    }

    internal static IEnumerable<uint> UnsupportedClasses() =>
        new uint[] { 0, uint.MaxValue, 10003 }.Concat(ObservedUnmodeled)
            .Concat(Families.SelectMany(family => new[] { family.First - 1, family.Last + 1 })).Distinct();

    internal static IEnumerable<object[]> StrictRefusals() =>
        UnsupportedClasses().Concat(WorldOnly).Distinct().Select(id => new object[] { id });
}
