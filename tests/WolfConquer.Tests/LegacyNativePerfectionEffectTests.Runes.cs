using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePerfectionEffectTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(325)]
    [InlineData(601)]
    public void AllSevenRealRuneLookupsAddTheirActualMutableItemLevelsAfterThresholds(int level)
    {
        using var fixture = new Fixture(eligible: true);
        for (int index = 0; index < Runes.Length; index++) fixture.AddRune(index);
        var expected = Expected(level);
        foreach (var rune in Runes) expected[rune.Field] += rune.Level;

        fixture.Client.PerfectionStatus.Update(level, fixture.Client);

        Assert.Equal(expected, Values(fixture.Client.PerfectionStatus));
        Assert.Equal(7, fixture.Storage.Objects.Length);
        foreach (var item in fixture.Storage.Objects) item.Position = 211;
        fixture.Client.PerfectionStatus.Update(0, fixture.Client);
        Assert.Equal(new double[16], Values(fixture.Client.PerfectionStatus));
        Assert.Equal(7, fixture.Storage.Objects.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void MissingMetadataGrantsNoBonusAndDoesNotInterruptValidRuneEffects(int failingRune)
    {
        using var fixture = new Fixture(eligible: true);
        var expected = Expected(601);
        for (int index = 0; index < failingRune; index++)
        {
            fixture.AddRune(index);
            expected[Runes[index].Field] += Runes[index].Level;
        }
        // The genuine ITEM_ID setter itself requires metadata. Construct through
        // that owner first, then remove the row to exercise the later rune lookup.
        LegacyNativePoolItems.ItemsBase![4_039_999] = new() { ID = 4_039_999, Name = "Transient fixture" };
        Assert.True(fixture.Storage.Add(new() { Uid = 99, ITEM_ID = 4_039_999, Position = 114 }));
        Assert.True(LegacyNativePoolItems.ItemsBase.Remove(4_039_999));
        Poison(fixture.Client.PerfectionStatus);

        fixture.Client.PerfectionStatus.Update(601, fixture.Client);

        Assert.Equal(expected, Values(fixture.Client.PerfectionStatus));
        Assert.False(LegacyNativePoolItems.ItemsBase!.ContainsKey(4_039_999));
        Assert.Equal(failingRune + 1, fixture.Storage.Objects.Length);
    }

    [Fact]
    public void UpdateUsesSuppliedClientsCurrentRuneOwnerAndRealAlternateSelection()
    {
        using var fixture = new Fixture(eligible: true);
        var first = fixture.AddRune(0);
        uint id = 4_031_109;
        LegacyNativePoolItems.ItemsBase![id] = new() { ID = id, Name = "Sanctity" };
        var alternate = new LegacyNativeGameItemRuntime { Uid = 99, ITEM_ID = id, Position = 123 };
        Assert.True(fixture.Storage.Add(alternate));
        var heldOwner = new LegacyNativePerfectionEffect();
        heldOwner.Update(1, fixture.Client);
        Assert.Equal(6d, heldOwner.DivineGuard);
        fixture.Client.Equipment!.Alternante = true;
        heldOwner.Update(1, fixture.Client);
        Assert.Equal(14d, heldOwner.DivineGuard);
        Assert.Equal(0d, fixture.Client.PerfectionStatus.DivineGuard);
        first.Position = 211;
        alternate.Position = 211;
        fixture.Client.Equipment = null;
        LegacyNativePoolItems.ItemsBase = null;
        heldOwner.Update(0, fixture.Client); // Excluded positions never dereference equipment/table.
        Assert.Equal(new double[16], Values(heldOwner));
    }

    [Fact]
    public void RefLevelDoesNotLeakFromSanctityIntoAbsentFireBlastAndSubsequentRunes()
    {
        using var fixture = new Fixture(eligible: true);
        fixture.AddRune(0);
        fixture.Client.PerfectionStatus.Update(0, fixture.Client);
        var expected = new double[16];
        expected[2] = 1;
        Assert.Equal(expected, Values(fixture.Client.PerfectionStatus));
    }
}
