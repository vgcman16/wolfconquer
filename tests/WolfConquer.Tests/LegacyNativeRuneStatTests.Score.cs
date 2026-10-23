using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using RuneTable = WolfConquer.Core.Data.LegacyNativeRuneTable;
using Stat = WolfConquer.Core.Data.LegacyNativeRuneTable.RuneAttribute;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRuneStatTests
{
    private static LegacyNativeGameItemRuntime[] PhysicalRuneCache(LegacyNativeRuneItemStorage rune) =>
        (LegacyNativeGameItemRuntime[])typeof(LegacyNativeRuneItemStorage)
            .GetField("objects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(rune)!;

    [Theory]
    [InlineData(0, 0u)]
    [InlineData(211, 0u)]
    [InlineData(212, 3200u)]
    [InlineData(101, 3200u)]
    [InlineData(121, 3200u)]
    [InlineData(65535, 3200u)]
    public void ScoreReadsEveryCachedObjectExceptExactlyZeroAnd211(int position, uint expected)
    {
        using var scope = new Scope();
        scope.Add(1, 4030009, (ushort)position);
        Assert.Equal(expected, scope.Rune.Score);
        scope.Client.Equipment!.Alternante = true;
        Assert.Equal(expected, scope.Rune.Score);
    }

    [Theory]
    [InlineData(4010001u, 500u)]
    [InlineData(4010099u, 500u)]
    [InlineData(4020000u, 500u)]
    [InlineData(4020027u, 8600u)]
    [InlineData(4030009u, 3200u)]
    [InlineData(7000099u, 30200u)]
    public void ScoreUsesOnlyNativeRedFamilyExceptionAndRawLastTwoDigits(uint id, uint expected)
    {
        using var scope = new Scope();
        scope.Add(1, id, 212);
        Assert.Equal(expected, scope.Rune.Score);
    }

    [Fact]
    public void ScoreUsesValidatedRowAliasesAndRejectsMalformedStorageWithoutRequiringEquippedOwnership()
    {
        using var scope = new Scope();
        var item = scope.Add(1, 4030003, 103);
        scope.Rune.Objects[0] = item;
        scope.Client.Equipment = null;
        scope.Rune.Owner = null;
        Assert.Equal(1400u, scope.Rune.Score);
        item.Position = 0;
        Assert.Equal(0u, scope.Rune.Score);
        var escaped = scope.Rune.Objects; escaped[0] = null!;
        Assert.Equal(0u, scope.Rune.Score); // Public arrays cannot corrupt storage.
        PhysicalRuneCache(scope.Rune)[0] = null!;
        Assert.Throws<InvalidDataException>(() => scope.Rune.Score);
    }

    [Fact]
    public void HpAddAccumulatesAllEligibleRowsAndOnlyExactHpSlotsWithNativeUIntOverflow()
    {
        using var scope = new Scope();
        scope.Add(1, 4010001, 101);
        RuneTable.Attributes[90] = Attribute(0, (Stat.HPAdd, uint.MaxValue), (Stat.None, 55), (Stat.AddAttack, 33));
        RuneTable.Attributes[2] = Attribute(500, (Stat.HPAdd, 3), ((Stat)255, 77));
        RuneTable.Attributes[1] = Attribute(501, (Stat.HPAdd, 100));
        Assert.Equal(2u, scope.Rune.HPAdd);
        scope.Rune.Objects[0].Position = 211;
        Assert.Equal(uint.MaxValue, scope.Rune.HPAdd);
        Assert.Equal(new uint[] { 90, 2, 1 }, RuneTable.Attributes.Keys);
    }

    [Fact]
    public void HpAddPreservesNativeRowSlotAndOwnerFaultsInsteadOfSkippingOrDefaulting()
    {
        using var scope = new Scope();
        RuneTable.Attributes[1] = new RuneTable.Attribute { Score = 1, AttributeValue = null };
        Assert.Equal(0u, scope.Rune.HPAdd); // Ineligible array is not evaluated.
        RuneTable.Attributes[1].Score = 0;
        Assert.Throws<ArgumentNullException>(() => scope.Rune.HPAdd);
        RuneTable.Attributes[1].AttributeValue = [null!];
        Assert.Throws<NullReferenceException>(() => scope.Rune.HPAdd);
        RuneTable.Attributes[1] = null!;
        Assert.Throws<NullReferenceException>(() => scope.Rune.HPAdd);
        RuneTable.Attributes = null!;
        Assert.Throws<NullReferenceException>(() => scope.Rune.HPAdd);
    }

    [Fact]
    public void HpAddReevaluatesActualScoreForEachEnumerationRatherThanUsingWornOrSavedScore()
    {
        using var scope = new Scope();
        var item = scope.Add(1, 4020027, 212); // Not worn but contributes 8600.
        RuneTable.Attributes[1] = Attribute(8600, (Stat.HPAdd, 47));
        scope.Client.Equipment = null;
        Assert.Equal(47u, scope.Rune.HPAdd);
        item.Position = 211;
        Assert.Equal(0u, scope.Rune.HPAdd);
        Assert.Equal(0u, scope.Rune.Score);
    }

    [Fact]
    public void RuneStatPlayerFieldsAreIndependentZeroBornPhysicalUInt32Fields()
    {
        using var scope = new Scope();
        var player = scope.Client.Player;
        Assert.Equal(0u, player.FineRainPower); Assert.Equal(0u, player.BloodTideHP);
        player.FineRainPower = uint.MaxValue; player.BloodTideHP = 123;
        Assert.Equal(uint.MaxValue, player.FineRainPower); Assert.Equal(123u, player.BloodTideHP);
        Assert.Equal(typeof(uint), typeof(LegacyNativePlayerRuntime).GetField(nameof(player.BloodTideHP))!.FieldType);
        Assert.Equal(typeof(uint), typeof(LegacyNativePlayerRuntime).GetField(nameof(player.FineRainPower))!.FieldType);
    }
}
