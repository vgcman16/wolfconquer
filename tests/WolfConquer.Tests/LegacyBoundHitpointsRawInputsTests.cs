using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBoundHitpointsRawInputsTests
{
    [Fact]
    public void CaptureRetainsDistinctOpaqueKeysAndWitnessesEveryInputAndOutputMap()
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            { ["Other"] = "one", ["other"] = "two", ["minhitpoints"] = "200" };
        var paid = new Dictionary<string, string>(StringComparer.Ordinal) { ["Opaque"] = "a", ["opaque"] = "b" };
        var before = OwnedEquipmentBehaviorFixture.Character(minHitPoints: 200) with
            { Attributes = attributes, PaidNobility = paid };
        var afterMap = new Dictionary<string, string>(attributes, StringComparer.Ordinal);
        var afterPaid = new Dictionary<string, string>(paid, StringComparer.Ordinal);
        var after = before with { MinHitPoints = 150, Attributes = afterMap, PaidNobility = afterPaid };
        var inputs = new WorldPacketHandlers.BoundHitpointsInputs(before, after);
        Assert.Equal("one", inputs.After.Attributes["Other"]); Assert.Equal("two", inputs.After.Attributes["other"]);
        Assert.Equal("150", inputs.After.Attributes["MinHitPoints"]);
        Assert.Equal("a", inputs.After.PaidNobility["Opaque"]); Assert.Equal("b", inputs.After.PaidNobility["opaque"]);
        inputs.Arm(); Assert.True(inputs.ConsumeBeforePlain());
        afterMap["other"] = "changed";
        Assert.False(inputs.ConsumeAfterPlain());
        ((Dictionary<string, string>)inputs.After.Attributes)["Other"] = "changed";
        Assert.False(inputs.ConsumePostludePlain());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AmbiguousOwnedScalarIsRejectedEvenWhenOpaqueCaseVariantsAreAllowed(bool ambiguousBefore)
    {
        var clean = new Dictionary<string, string>(StringComparer.Ordinal) { ["Other"] = "1", ["other"] = "2" };
        var duplicate = new Dictionary<string, string>(clean, StringComparer.Ordinal)
            { ["MinHitPoints"] = "200", ["minhitpoints"] = "200" };
        var character = OwnedEquipmentBehaviorFixture.Character(minHitPoints: 200);
        var before = character with { Attributes = ambiguousBefore ? duplicate : clean };
        var after = character with { Attributes = ambiguousBefore ? clean : duplicate };
        Assert.Throws<InvalidOperationException>(() => new WorldPacketHandlers.BoundHitpointsInputs(before, after));
    }
}
