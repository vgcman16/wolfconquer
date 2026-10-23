namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public void CodeFactorIndexedAliasesReferToOneIntentionalBackDamageRollCell()
    {
        Assert.Equal("LegacyHeavensWonderBackDamageRoll", CounterKill16350BackDamageRollAttribute);
        Assert.Equal(CounterKill16350BackDamageRollAttribute, ManiacDanceHeavensWonderRollAttribute);
    }

    // Called at the actual fixture initializer in all71 existing cases across the three files.
    // No new combat callback, packet projection or shortened gameplay test is introduced.
    private static void AssertCodeFactorIndexedSharedRoll(IReadOnlyDictionary<string, string> attributes)
    {
        var entry = Assert.Single(attributes,
            pair => string.Equals(pair.Key, CounterKill16350BackDamageRollAttribute, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("0", entry.Value);
        Assert.Equal("0", attributes[ManiacDanceHeavensWonderRollAttribute]);
    }
}
