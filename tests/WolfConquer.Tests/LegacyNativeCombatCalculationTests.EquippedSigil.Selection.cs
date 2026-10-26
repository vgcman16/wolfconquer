using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PhysicalSigilRetainsEveryPrimaryOrAlternateSlotAndLastMatchingSlot(bool alternate)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s, 2);
        Set(s.Player, "_class", 5000u);
        s.Client.Status.CriticalStrike = 15000;
        s.Client.Equipment!.Alternante = alternate;
        for (uint slot = 1; slot <= 8; slot++)
        {
            s.Client.MyNinja!.Items[900 + slot] = new() { ItemID = 404, Position = alternate ? slot + 8 : slot, Level = slot };
            LegacyNativeNinjaFile.gouyu_type[700 + slot] = new() { ItemID = 404, Level = slot, Power = -1, Damage = slot * 10 };
            AssertPhysicalSigilMatchesActualSelectedValues(s, monster);
        }
    });

    [Fact]
    public Task PhysicalSigilRetainsAlternatePairFallbackAndFirstExactMetadataMatch()
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s, 2);
        Set(s.Player, "_class", 5000u);
        s.Client.Status.CriticalStrike = 15000;
        s.Client.Equipment!.Alternante = true;
        AddPhysicalSelectionSigil(s, 1, 1, 10);
        AddPhysicalSelectionSigil(s, 9, 2, 20);
        AddPhysicalSelectionSigil(s, 2, 3, 30);
        AddPhysicalSelectionSigil(s, 10, 4, 99, itemId: 403);
        AssertPhysicalSigilMatchesActualSelectedValues(s, monster);
        AddPhysicalSelectionSigil(s, 10, 5, 40);
        AssertPhysicalSigilMatchesActualSelectedValues(s, monster);
        AddPhysicalSelectionSigil(s, 8, 6, 50);
        AssertPhysicalSigilMatchesActualSelectedValues(s, monster);
        AddPhysicalSelectionSigil(s, 16, 7, 60);
        AssertPhysicalSigilMatchesActualSelectedValues(s, monster);
        LegacyNativeNinjaFile.gouyu_type = new()
        {
            [12345] = new() { ItemID = 404, Level = 7, Power = int.MinValue, Damage = 60 },
            [54321] = new() { ItemID = 404, Level = 7, Power = int.MaxValue, Damage = 99 },
        };
        AssertPhysicalSigilMatchesActualSelectedValues(s, monster);
    });

    [Fact]
    public Task PhysicalSigilRetainsFirstValuesMatchWhenOneSlotContainsSeveralCandidates()
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s, 2);
        Set(s.Player, "_class", 5000u);
        s.Client.Status.CriticalStrike = 15000;
        AddPhysicalSelectionSigil(s, 8, 1, 10);
        AddPhysicalSelectionSigil(s, 8, 2, 20);
        AssertPhysicalSigilMatchesActualSelectedValues(s, monster);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PhysicalNoSelectedSigilDoesNotReadOptionalMissingItemMetadata(bool nonNinja)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s);
        Set(s.Player, "_class", nonNinja ? 2000u : 5000u);
        LegacyNativeNinjaFile.gouyu_type = null!;
        if (nonNinja) s.Client.MyNinja!.Items = null!;

        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster,
            new LegacyNativeMagicType.Magic { ID = 16430, Damage = 100 }, out var result);

        Assert.Equal(1200u, result.Damage);
        Assert.Empty(s.Queue);
        AssertPhysicalAdmissionFourDraws();
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PhysicalSigilCaptureRejectsAboveItsExplicitWorkBudgetBeforeDirtyPrefix(bool metadata)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s);
        Set(s.Player, "_class", 5000u);
        InstallPhysicalWhirlSigil(s);
        if (metadata)
        {
            for (uint index = 0; index < 16384; index++)
                LegacyNativeNinjaFile.gouyu_type[10000 + index] = new() { ItemID = 403, Level = 2 };
        }
        else
        {
            for (uint index = 0; index < 4096; index++)
                s.Client.MyNinja!.Items[10000 + index] = new() { ItemID = 403, Position = 0, Level = 1 };
        }
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);
        var sentinel = PhysicalAdmissionSentinel();
        var result = sentinel;

        Assert.Throws<InvalidDataException>(() => LegacyNativeCalculatePhysical.OnMonster(s.Player, monster,
            new LegacyNativeMagicType.Magic { ID = 16420, Damage = 100 }, out result));

        Assert.Same(sentinel, result);
        prefix.AssertUnchanged(s);
        Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });

    private static void AddPhysicalSelectionSigil(Scope scope, uint position, uint level, uint damage, uint itemId = 404)
    {
        scope.Client.MyNinja!.Items[900 + level] = new() { ItemID = itemId, Position = position, Level = level };
        LegacyNativeNinjaFile.gouyu_type[700 + level] = new() { ItemID = itemId, Level = level, Power = -1, Damage = damage };
    }

    private static void AssertPhysicalSigilMatchesActualSelectedValues(Scope scope, LegacyNativeMonsterRole monster)
    {
        Assert.True(scope.Client.MyNinja!.TryGetValueEquip(LegacyNativeNinja.ItemType.WhirlSigilSage, out var selected));
        var expected = unchecked(2400u + 2400u * selected.DBItem.Damage / 100u);
        LegacyNativeAttackBase.MyRandom = new LegacyNativeSafeRandom(2);

        LegacyNativeCalculatePhysical.OnMonster(scope.Player, monster,
            new LegacyNativeMagicType.Magic { ID = 16420, Damage = 100 }, out var result);

        Assert.Equal(expected, result.Damage);
        Assert.Equal(LegacyNativeMsgAttackPacket.AttackEffect.CriticalStrike, result.Effect);
        Assert.Empty(scope.Queue);
    }
}
