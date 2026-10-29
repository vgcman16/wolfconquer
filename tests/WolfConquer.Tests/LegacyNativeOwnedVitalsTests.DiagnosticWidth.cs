using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeOwnedVitalsTests
{
    [Theory]
    [InlineData(65535U)] [InlineData(65536U)] [InlineData(uint.MaxValue)]
    public void DiagnosticCodecPreservesWideCurrentManaWithoutPromotingItsProofFlags(uint mana)
    {
        var result = new LegacyRuntimeInspectionResult(1, Guid.NewGuid(), LegacyRuntimeInspectionStatus.Observed, "diagnostic",
            new(Guid.NewGuid(), 1, 2, DateTimeOffset.UtcNow, "VisibleDx8D13",
                new(2, 1002, 1, 2, 1008, 1000, 120, 100, 50, 9, mana, uint.MaxValue),
                new(2, 9, mana), [], [], true));
        var bytes = Encoding.UTF8.GetBytes(LocalRuntimeInspectionProtocol.FormatResult(result));
        var roundtrip = LocalRuntimeInspectionProtocol.ParseResult(bytes);
        Assert.Equal(mana, roundtrip.Snapshot!.NativeVitals.Mana);
        Assert.Equal((long)mana, roundtrip.Snapshot.ManagedCharacter.MinMana);
        Assert.False(roundtrip.AtomicNativeTupleProven); Assert.False(roundtrip.LiveStoreSynchronized);
        foreach (var invalid in new long[] { -1, (long)uint.MaxValue + 1 })
        {
            var node = JsonNode.Parse(bytes)!; node["snapshot"]!["nativeVitals"]!["mana"] = invalid;
            Assert.Throws<JsonException>(() => LocalRuntimeInspectionProtocol.ParseResult(Encoding.UTF8.GetBytes(node.ToJsonString())));
        }
    }
}
