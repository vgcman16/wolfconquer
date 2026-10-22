using WolfConquer.Core.World;
using Attr = WolfConquer.Core.Data.LegacyNativeHundredWeaponsTable.AttributeType;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredWeaponsTests
{
    [Theory]
    [InlineData(0u, 1500u, 50.0, 15, 1.0, 50, 0.2, 90)]
    [InlineData(1501u, 2000u, 55.0, 15, 1.5, 50, 0.2, 90)]
    [InlineData(2001u, 2500u, 55.0, 15, 1.5, 50, 0.2, 85)]
    [InlineData(2501u, 3000u, 60.0, 15, 2.0, 45, 0.4, 80)]
    [InlineData(3001u, 5000u, 70.0, 15, 3.0, 40, 0.6, 70)]
    [InlineData(5001u, 10000u, 80.0, 15, 4.0, 35, 0.8, 60)]
    [InlineData(10001u, 15000u, 90.0, 15, 5.0, 30, 1.0, 55)]
    [InlineData(15001u, 30000u, 100.0, 10, 10.0, 25, 1.2, 50)]
    [InlineData(30001u, 40000u, 100.0, 8, 12.0, 20, 1.4, 45)]
    [InlineData(40001u, 60000u, 100.0, 6, 15.0, 20, 1.6, 40)]
    [InlineData(60001u, 80000u, 100.0, 4, 20.0, 20, 1.8, 35)]
    [InlineData(80001u, 100000u, 100.0, 2, 25.0, 20, 2.0, 30)]
    [InlineData(100001u, 130000u, 100.0, 1, 28.0, 20, 2.2, 30)]
    [InlineData(130001u, uint.MaxValue, 100.0, 0, 30.0, 20, 2.5, 30)]
    public Task AllThreeRateTablesUseInclusiveNativeBoundsAndLiveRealScore(uint low, uint high,
        double first, byte firstSeconds, double second, byte secondSeconds, double third, byte thirdSeconds) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = scope.Item(Weapon.Blade); scope.Meta(Weapon.Blade, 1); scope.Owner.Unlocked = true;
        foreach (uint score in new[] { low, high })
        {
            // Physical signed attribute + genuine unchecked Score arithmetic, not a result provider.
            row.Attributes[Attr.Hitpoints] = unchecked((int)((score - 1000u) * 3435973837u));
            Assert.Equal(score, row.Score); Assert.Equal(score, scope.Owner.TotalScore);
            var a = Assert.Single(scope.Owner.GetWeaponRate(1)); Assert.Equal(first, a.Key); Assert.Equal(firstSeconds, a.Value);
            var b = Assert.Single(scope.Owner.GetWeaponRate(2)); Assert.Equal(second, b.Key); Assert.Equal(secondSeconds, b.Value);
            var c = Assert.Single(scope.Owner.GetWeaponRate(3)); Assert.Equal(third, c.Key); Assert.Equal(thirdSeconds, c.Value);
        }
        Assert.Null(scope.Owner.GetWeaponRate(0)); Assert.Null(scope.Owner.GetWeaponRate(4));
    });

    [Fact]
    public Task StagesCountMatchingRowsNotDistinctSlotsAndDoNotRequireUnlockOrValidClass() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.SetClass(5000);
        Weapon[] keys = [Weapon.Blade, Weapon.Sword, Weapon.Hook, Weapon.Whip, Weapon.Axe];
        Assert.False(scope.Owner.StageActivated(1));
        for (int index = 0; index < keys.Length; index++)
        {
            var row = scope.Item(keys[index]); row.BitVector.Add(2); row.BitVector.Add(5);
            Assert.Equal(index >= 2, scope.Owner.StageActivated(2)); Assert.Equal(index >= 4, scope.Owner.StageActivated(3));
        }
        scope.Owner.Objects[Weapon.Blade].BitVector.Add(1);
        Assert.True(scope.Owner.StageActivated(1)); Assert.False(scope.Owner.StageActivated(0)); Assert.False(scope.Owner.StageActivated(4));
        Assert.False(scope.Owner.Unlocked); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task NextSpellConsumesActualMutableFifoIncludingZeroAndDuplicates() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Owner.TODO_Spells.AddRange([17, 0, 17]);
        var list = scope.Owner.TODO_Spells;
        Assert.Equal((ushort)17, scope.Owner.GetNextSpell()); Assert.Equal((ushort)0, scope.Owner.GetNextSpell());
        Assert.Equal((ushort)17, scope.Owner.GetNextSpell()); Assert.Equal((ushort)0, scope.Owner.GetNextSpell());
        Assert.Same(list, scope.Owner.TODO_Spells); Assert.Empty(list);
        scope.Owner.TODO_Spells = null!; Assert.Throws<NullReferenceException>(() => scope.Owner.GetNextSpell());
    });

    [Fact]
    public Task TriggerUsesRealClockAndDurationYetAlwaysReturnsFalse() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Owner.TriggerStamp[2] = new LegacyNativeTime32(int.MinValue);
        Assert.True(scope.Owner.CanTrigger(2));
        var before = LegacyNativeTime32.Now;
        Assert.False(scope.Owner.Trigger(2));
        var after = LegacyNativeTime32.Now; var stamp = scope.Owner.TriggerStamp[2];
        Assert.InRange(unchecked((uint)(stamp.Value - before.Value)), 0u, unchecked((uint)(after.Value - before.Value)));
        Assert.Equal((byte)50, scope.Owner.TriggerSeconds[2]);
        Assert.Equal(Hex("10003109080410001800200028023032"), Assert.Single(scope.Queue));
        Assert.Equal(15, scope.Available);
    });

    [Fact]
    public Task FutureSignedTickGateDoesNotMutateOrSerialize() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var future = new LegacyNativeTime32(int.MaxValue);
        scope.Owner.TriggerStamp[2] = future; scope.Owner.TriggerSeconds[2] = 0;
        Assert.True(LegacyNativeTime32.Now < future);
        Assert.False(scope.Owner.CanTrigger(2)); Assert.False(scope.Owner.Trigger(2));
        Assert.True(LegacyNativeTime32.Now < future);
        Assert.Equal(future, scope.Owner.TriggerStamp[2]); Assert.Equal((byte)0, scope.Owner.TriggerSeconds[2]);
        Assert.Empty(scope.Queue); Assert.Equal(16, scope.Available);
    });

    [Fact]
    public Task TriggerMissingOrUnknownStageAndRecycleFaultRetainNativePartialOrder() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Throws<KeyNotFoundException>(() => scope.Owner.CanTrigger(1));
        scope.Owner.TriggerStamp[0] = new LegacyNativeTime32(int.MinValue); scope.Owner.TriggerSeconds[0] = 0;
        Assert.Throws<ArgumentNullException>(() => scope.Owner.Trigger(0));
        Assert.Equal(int.MinValue, scope.Owner.TriggerStamp[0].Value); Assert.Equal((byte)0, scope.Owner.TriggerSeconds[0]);
        scope.Owner.TriggerStamp[3] = new LegacyNativeTime32(int.MinValue); scope.NullBin();
        var before = LegacyNativeTime32.Now;
        Assert.Throws<NullReferenceException>(() => scope.Owner.Trigger(3));
        var after = LegacyNativeTime32.Now; scope.RestoreBin();
        Assert.Equal((byte)90, scope.Owner.TriggerSeconds[3]);
        Assert.InRange(unchecked((uint)(scope.Owner.TriggerStamp[3].Value - before.Value)), 0u, unchecked((uint)(after.Value - before.Value)));
        Assert.Empty(scope.Queue); Assert.Equal(16, scope.Available);
    });
}
