using WolfConquer.Core.World;
using Setting = WolfConquer.Core.World.LegacyNativeMonsterSettings;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterAttackTests
{
    [Theory]
    [InlineData("Pheasant", 0x35)]
    [InlineData("Guard", 0xA4)]
    [InlineData("ChaosGuard", 0x35)]
    [InlineData("ChaosCitySoldier", 0xA4)]
    [InlineData("ReviverKing", 0x62)]
    [InlineData("GuardReviver", 0xA4)]
    [InlineData("ChaosGuardKing", 0x135)]
    [InlineData("KingMessenger", 0x135)]
    [InlineData("Messenger", 0x435)]
    [InlineData("guard", 0x35)]
    public Task FamilySettingsPreserveCompositeValuesAndOrdinalPriority(string name, uint settings) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var family = Family(); family.Name = name; family.CreateMonsterSettings(); Assert.Equal(settings, (uint)family.Settings);
    });

    [Fact]
    public Task FamilyBirthAndFaultPrefixAreNative() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var family = new LegacyNativeMonsterFamily();
        Assert.Null(family.Name); Assert.Null(family.DropSpecials); Assert.Null(family.ItemGenerator); Assert.Equal(0, family.MaxHealth);
        family.Settings = Setting.Invincible; Assert.Throws<NullReferenceException>(() => family.CreateMonsterSettings()); Assert.Equal(Setting.Standard, family.Settings);
        var control = new Random(21); LegacyNativeMonsterFamily.Random = new Random(21);
        Assert.Throws<NullReferenceException>(() => family.Copy()); control.Next(10,20); Assert.Equal(control.Next(), LegacyNativeMonsterFamily.Random.Next());
        Assert.Null(family.ItemGenerator);
    });

    [Theory]
    [InlineData(300, 100000, 1, 0)]
    [InlineData(300, 100001, 1, 3600)]
    [InlineData(300, 6999999, 1, 3600)]
    [InlineData(300, 7000000, 1, 0)]
    [InlineData(300, 200000, 0, 0)]
    [InlineData(20211, 200000, 1, -1)]
    [InlineData(2700, 200000, 1, 71)]
    [InlineData(2699, 500, 0, 71)]
    [InlineData(7022, 500, 0, 71)]
    [InlineData(2164, 500, 0, 71)]
    [InlineData(2165, 500, 0, 71)]
    [InlineData(20055, 500, 0, 3600)]
    [InlineData(4171, 500, 0, 3600)]
    [InlineData(32070, 500, 0, 3600)]
    public Task CopyConsumesExactFamilyRngAndPreservesRespawnOverrideOrder(uint id, int maxHp, byte boss, int expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var f = Family(id); f.MaxHealth = maxHp; f.Boss = boss; f.rest_secs = 71;
        var rng = new Random(23); LegacyNativeMonsterFamily.Random = new Random(23); int ordinary = rng.Next(10,20); int special = id == 20211 ? rng.Next(100,200) : 0;
        var copy = f.Copy(); Assert.Equal(expected == 0 ? ordinary : expected == -1 ? special : expected, copy.RespawnTime);
        Assert.Equal(maxHp > 100000 && maxHp < 7000000 && boss > 0 ? 12 : 2, copy.AttackRange);
        Assert.Equal(rng.Next(), LegacyNativeMonsterFamily.Random.Next());
    });

    [Fact]
    public Task CopyOmitsOriginalOmittedFieldsAndSharesWatcherAndGeneratorReferences() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var f = Family(301); f.Name = "King"; f.DropArmet = 99; f.rest_secs = 12; f.SpawnCount = 8; f.maxnpc = 9;
        f.extra_battlelev = 55; f.extra_damage = 56; f.extra_exp = 57; f.ExtraCritical = 58; f.ExtraBreack = 59;
        f.DropSpecials = [new(711u,2)]; var first = f.Copy(); var second = f.Copy();
        Assert.NotSame(f.DropSpecials, first.DropSpecials); Assert.Same(f.DropSpecials[0].Rate, first.DropSpecials![0].Rate);
        Assert.False((bool)first.DropSpecials[0].Rate!); Assert.True((bool)second.DropSpecials![0].Rate!);
        Assert.Same(first.ItemGenerator, second.ItemGenerator); Assert.Null(f.ItemGenerator);
        Assert.Equal(0, first.DropArmet); Assert.Equal(0, first.rest_secs); Assert.Equal(0, first.SpawnCount); Assert.Equal(0, first.SpawnX); Assert.Equal(0, first.maxnpc);
        Assert.Equal(55, first.extra_battlelev); Assert.Equal(56, first.extra_damage); Assert.Equal(57, first.extra_exp); Assert.Equal(58u,first.ExtraCritical); Assert.Equal(59u,first.ExtraBreack);
        Assert.Equal(Setting.King, first.Settings);
        var other = Family(301); other.DropHPItem = 987; other.CreateItemGenerator(); Assert.Same(first.ItemGenerator, other.ItemGenerator);
        Assert.Same(first, typeof(LegacyNativeMobItemGenerator).GetField("Family", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(other.ItemGenerator));
    });

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(10)] [InlineData(0)] [InlineData(-1)]
    public Task RateWatcherIsAnIncrementingCounterNotRandom(int tick) => NativePacketTestThread.Run(() =>
    {
        var watcher = new LegacyNativeMobRateWatcher(tick);
        for (int i = 1; i <= 31; i++) Assert.Equal(tick > 0 && i % tick == 0, (bool)watcher);
        Assert.Throws<NullReferenceException>(() => { _ = (bool)(LegacyNativeMobRateWatcher)null!; });
    });
}
