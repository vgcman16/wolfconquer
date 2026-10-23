using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapSpawnTests
{
    private static string MonsterIni(uint id = 111, string name = "Pheasant", int level = 15,
        int min = 10, int max = 30, int boss = 0, string extra = "") =>
        $"[cq_monstertype]\r\nid={id}\r\nname={name}\r\nlevel={level}\r\nattack_min={min}\r\nattack_max={max}\r\nBoss={boss}\r\n{extra}";

    [Fact]
    public Task WholeFamilyDatabaseLoaderPreservesAllMetadataAndRealSpecialDropGeneratorOwners() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles();
        scope.WriteDb(Path.Combine("Monsters", "owned.data"), MonsterIni(extra:
            "defence=17\r\nlookface=19\r\nlife=-123\r\nattack_range=-3\r\ndodge=23\r\n" +
            "drop_shoes=1\r\ndrop_necklace=2\r\ndrop_ring=3\r\ndrop_armet=4\r\ndrop_armor=5\r\ndrop_shield=6\r\ndrop_weapon=7\r\n" +
            "drop_money=65535\r\ndrop_hp=1000030\r\ndrop_mp=1001030\r\ndefence2=-9\r\nmove_speed=31\r\nattack_speed=37\r\nmagic_type=42\r\n" +
            "critical=43\r\nbreak=47\r\nextra_battlelev=-2\r\nextra_exp=53\r\nextra_damage=-59\r\n[SpecialDrop]\r\nCount=1\r\n0=711001,2\r\n"));
        LegacyNativeServerMapPopulation.LoadMonsters();
        var family = Assert.Single(LegacyNativePoolMonsters.MonsterFamilies).Value;
        Assert.Equal(111u, family.ID); Assert.Equal("Pheasant", family.Name); Assert.Equal((ushort)15, family.Level);
        Assert.Equal(10, family.MinAttack); Assert.Equal(30, family.MaxAttack); Assert.Equal(-123, family.MaxHealth);
        Assert.Equal((ushort)17, family.Defense); Assert.Equal((ushort)19, family.Mesh); Assert.Equal((byte)16, family.ViewRange);
        Assert.Equal((sbyte)-3, family.AttackRange); Assert.Equal((byte)23, family.Dodge);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7 }, new[] { family.DropBoots, family.DropNecklace, family.DropRing,
            family.DropArmet, family.DropArmor, family.DropShield, family.DropWeapon });
        Assert.Equal(ushort.MaxValue, family.DropMoney); Assert.Equal(1000030u, family.DropHPItem); Assert.Equal(1001030u, family.DropMPItem);
        Assert.Equal(-9, family.Defense2); Assert.Equal(31, family.MoveSpeed); Assert.Equal(37, family.AttackSpeed); Assert.Equal(42u, family.SpellId);
        Assert.Equal(43u, family.ExtraCritical); Assert.Equal(47u, family.ExtraBreack);
        Assert.Equal(-2, family.extra_battlelev); Assert.Equal(53, family.extra_exp); Assert.Equal(-59, family.extra_damage);
        var drop = Assert.Single(family.DropSpecials!); Assert.Equal(711001u, drop.ID); Assert.NotNull(drop.Rate);
        Assert.False((bool)drop.Rate!); Assert.True((bool)drop.Rate!); Assert.False((bool)drop.Rate!);
        Assert.NotNull(family.ItemGenerator); Assert.Equal(LegacyNativeMonsterSettings.Standard, family.Settings);
        var copy = family.Copy(); Assert.Same(family.ItemGenerator, copy.ItemGenerator); Assert.Same(drop.Rate, Assert.Single(copy.DropSpecials!).Rate);
    });

    [Theory]
    [InlineData(3000, -101, 0, 3000, -101, -3)]
    [InlineData(3001, -101, 0, 1500, -50, -3)]
    [InlineData(3001, 101, 1, 3001, 101, 3)]
    public Task FamilyLoaderKeepsExactAttackHalvingThresholdAndBossRange(int max, int min, int boss,
        int expectedMax, int expectedMin, int range) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles();
        scope.WriteDb(Path.Combine("Monsters", "one.ini"), MonsterIni(min: min, max: max, boss: boss, extra: "attack_range=-3\r\n"));
        LegacyNativeServerMapPopulation.LoadMonsters(); var family = Assert.Single(LegacyNativePoolMonsters.MonsterFamilies).Value;
        Assert.Equal(expectedMax, family.MaxAttack); Assert.Equal(expectedMin, family.MinAttack); Assert.Equal((sbyte)range, family.AttackRange);
    });

    [Theory]
    [InlineData(0u, "Pheasant", 15, 10, 30)] [InlineData(111u, "INVALID_MOB", 15, 10, 30)]
    [InlineData(111u, "Pheasant", 0, 10, 30)] [InlineData(111u, "Pheasant", 15, 31, 30)]
    public Task NativeInvalidFamilyPredicateSkipsOnlyThatFile(uint id, string name, int level, int min, int max) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles();
        scope.WriteDb(Path.Combine("Monsters", "bad.ini"), MonsterIni(id, name, level, min, max));
        scope.WriteDb(Path.Combine("Monsters", "good.ini"), MonsterIni(222));
        LegacyNativeServerMapPopulation.LoadMonsters(); Assert.Equal(222u, Assert.Single(LegacyNativePoolMonsters.MonsterFamilies).Key);
    });

    [Theory]
    [InlineData("CitySoldier", 0xA4u)] [InlineData("Guard", 0xA4u)] [InlineData("Reviver", 0x62u)]
    [InlineData("King", 0x135u)] [InlineData("Messenger", 0x435u)]
    public Task LoaderCallsWholeNativeSettingsNameClassifier(string name, uint settings) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); scope.WriteDb(Path.Combine("Monsters", "one.ini"), MonsterIni(name: name));
        LegacyNativeServerMapPopulation.LoadMonsters();
        Assert.Equal(settings, (uint)Assert.Single(LegacyNativePoolMonsters.MonsterFamilies).Value.Settings);
    });

    [Fact]
    public Task FamilyLoadFailurePreservesEarlierRealRowsAndDoesNotContinueAfterMalformedSpecialDrop() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles();
        for (int i = 0; i < 3; i++) scope.WriteDb(Path.Combine("Monsters", $"owned-{i}.ini"), "");
        string[] order = Directory.GetFiles(Path.Combine(scope.Db, "Monsters"));
        File.WriteAllText(order[0], MonsterIni(111));
        File.WriteAllText(order[1], MonsterIni(222, extra: "[SpecialDrop]\r\nCount=1\r\n0=bad,2\r\n"));
        File.WriteAllText(order[2], MonsterIni(333));
        LegacyNativeServerMapPopulation.LoadMonsters();
        Assert.Equal(111u, Assert.Single(LegacyNativePoolMonsters.MonsterFamilies).Key);
        LegacyNativeServerMapPopulation.LoadMonsters(); // Duplicate Add also ends the whole native loop.
        Assert.Equal(111u, Assert.Single(LegacyNativePoolMonsters.MonsterFamilies).Key);
    });
}
