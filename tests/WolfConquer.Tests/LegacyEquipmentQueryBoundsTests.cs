using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyEquipmentQueryBoundsTests
{
    [Fact]
    public void SacrificeUsesWideProductAndPreservesIntegerRounding()
    {
        Assert.Equal(99U, LegacyEquipmentQueryBounds.ReduceHitpoints(147, 33));
        Assert.Equal(uint.MaxValue - (uint)((ulong)uint.MaxValue * 99 / 100),
            LegacyEquipmentQueryBounds.ReduceHitpoints(uint.MaxValue, 99));
        Assert.Throws<InvalidDataException>(() => LegacyEquipmentQueryBounds.ReduceHitpoints(100, 101));
    }

    [Fact]
    public void ClassSpiritManaRejectsNegativeAndNativeWidthOverflow()
    {
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 13_005) with { Spirit = 2_184 };
        Assert.Equal((ushort)65_520, LegacyEquipmentNativeMana.ClassAndSpirit(character));
        Assert.Throws<OverflowException>(() => LegacyEquipmentNativeMana.ClassAndSpirit(character with { Spirit = 2_185 }));
        Assert.Throws<InvalidDataException>(() => LegacyEquipmentNativeMana.ClassAndSpirit(character with { Spirit = -1 }));
    }

    [Fact]
    public void IntermediateHpAdditionAndClampCannotBeHiddenByLaterTransform()
    {
        var hp = new LegacyEquipmentQueryHitpointReplay(int.MaxValue);
        Assert.Throws<OverflowException>(() => hp.AddCurrent(1));
        Assert.Equal(int.MaxValue, hp.Current);
        hp = new(30); hp.AddMaximum((uint)int.MaxValue + 1);
        Assert.Throws<OverflowException>(hp.ClampCurrent);
        hp = new(30); hp.AddMaximum(60); hp.AddCurrent(5); hp.ClampCurrent(); hp.Transform(100);
        Assert.Equal(100, hp.Current); Assert.Equal(100U, hp.Maximum);
    }

    [Fact]
    public void HpProjectionPreservesOpaqueComparerAndRejectsOnlyOwnedDuplicates()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        { ["Other"] = "one", ["other"] = "two", ["minhitpoints"] = "30" };
        var before = OwnedEquipmentBehaviorFixture.Character(minHitPoints: 30) with { Attributes = fields };
        var after = LegacyEquipmentQueryBounds.WithHitpoints(before, 35);
        var actual = Assert.IsType<Dictionary<string, string>>(after.Attributes);
        Assert.Same(StringComparer.Ordinal, actual.Comparer);
        Assert.Equal("one", actual["Other"]); Assert.Equal("two", actual["other"]);
        Assert.Equal("35", actual["minhitpoints"]); Assert.False(actual.ContainsKey("MinHitPoints"));
        Assert.Equal("30", fields["minhitpoints"]);
        fields["MinHitPoints"] = "30";
        Assert.Throws<InvalidDataException>(() => LegacyEquipmentQueryBounds.WithHitpoints(before, 35));
    }

    [Fact]
    public void MagicCumulativeAndPercentageOverflowRefuseWithoutWrappedResult()
    {
        var almost = new LegacyNativeMagicQueryFields(uint.MaxValue - 15, 7, 9, 11);
        var exact = LegacyEquipmentMagicArithmetic.Add(almost, new(15, 1, 2, 3));
        Assert.Equal(uint.MaxValue, exact.MagicAttack); Assert.Equal(8U, exact.MagicPercent);
        Assert.Throws<OverflowException>(() => LegacyEquipmentMagicArithmetic.Add(almost, new(16, 0, 0, 0)));
        Assert.Throws<OverflowException>(() => LegacyEquipmentMagicArithmetic.NatureChant(almost, 9));
        Assert.Equal(124U, LegacyEquipmentMagicArithmetic.NatureChant(new(100, 0, 0, 0), 9).MagicAttack);
        Assert.Equal((ushort)1000, LegacyEquipmentQueryBounds.ClampVigor(2000, 1000));
        Assert.Throws<OverflowException>(() => LegacyEquipmentQueryBounds.ClampVigor(uint.MaxValue, 1000));
    }
}
