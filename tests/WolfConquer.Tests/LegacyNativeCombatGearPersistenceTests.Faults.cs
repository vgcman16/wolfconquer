using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearPersistenceTests
{
    [Fact]
    public void ShortAnimaRowFaultRetainsScalarAndEarlierArrayWritesWithoutReachingJadesOrBag()
    {
        using var f = new PersistenceFixture();
        var ini = f.Ini(1);
        ini.Write("CombatGear", "Count", 7);
        ini.Write("Item0", "Animas[1]2", 777);
        ini.Write("Item0", "BlackJades0", 778);
        ini.Write("BlackJadeBag", "Count", 9);
        var gear = new Gear { MasteryPoints = 71, TaoistReiki = 72, PirateExp = 73, Lottery = 74 };
        gear.AirPowers.Points = 75;
        var item = Weapon(201, 3, 100);
        var shortRow = new uint[] { 901, 902 };
        item.TypeAnima[1] = shortRow;
        gear.Items[item.ItemID] = item;

        Assert.Throws<IndexOutOfRangeException>(() => gear.Save(1));

        Assert.Equal((71u, 72u, 73u, 74u), (ini.ReadUInt32("CombatGear", "MasteryPoints"),
            ini.ReadUInt32("CombatGear", "TaoistReiki"), ini.ReadUInt32("CombatGear", "PirateExp"),
            ini.ReadUInt32("CombatGear", "Lottery")));
        Assert.Equal(1, ini.ReadInt32("CombatGear", "Count", -1));
        Assert.Equal(75u, ini.ReadUInt32("AirPowers", "Points"));
        Assert.Equal((201u, 3u, 101u, 102u, 103u, 104u), (ini.ReadUInt32("Item0", "ItemID"),
            ini.ReadUInt32("Item0", "Level"), ini.ReadUInt32("Item0", "Exp"), ini.ReadUInt32("Item0", "Equip"),
            ini.ReadUInt32("Item0", "Transform"), ini.ReadUInt32("Item0", "MasteryScore")));
        for (var j = 0; j < 7; j++) Assert.Equal(110u + (uint)j, ini.ReadUInt32("Item0", "Animas[0]" + j));
        Assert.Equal(901u, ini.ReadUInt32("Item0", "Animas[1]0"));
        Assert.Equal(902u, ini.ReadUInt32("Item0", "Animas[1]1"));
        Assert.Equal(777u, ini.ReadUInt32("Item0", "Animas[1]2"));
        Assert.Equal(778u, ini.ReadUInt32("Item0", "BlackJades0"));
        Assert.Equal(9, ini.ReadInt32("BlackJadeBag", "Count", -1));
        Assert.Same(item, Assert.Single(gear.Items).Value);
        Assert.Same(shortRow, item.TypeAnima[1]);
        Assert.Equal(new uint[] { 901, 902 }, shortRow);
    }

    [Fact]
    public void NullThirdJadeFaultRetainsAllAnimasAndTwoJadeWritesWithoutReachingBagCount()
    {
        using var f = new PersistenceFixture();
        var ini = f.Ini(2);
        ini.Write("Item0", "BlackJades2", 999);
        ini.Write("Item0", "BlackJades3", 888);
        ini.Write("BlackJadeBag", "Count", 7);
        var gear = new Gear { MasteryPoints = 81 };
        var item = Weapon(202, 4, 200);
        var jades = new Gear.BlackJade[] { new(501, 9), new(502, 10), null! };
        item.BlackJades = jades;
        gear.Items[item.ItemID] = item;
        gear.BlackJadeBag[113] = new(113, 33);

        Assert.Throws<NullReferenceException>(() => gear.Save(2));

        Assert.Equal(81u, ini.ReadUInt32("CombatGear", "MasteryPoints"));
        Assert.Equal(1, ini.ReadInt32("CombatGear", "Count", -1));
        Assert.Equal(202u, ini.ReadUInt32("Item0", "ItemID"));
        Assert.Equal(204u, ini.ReadUInt32("Item0", "MasteryScore"));
        for (var i = 0; i < 2; i++)
            for (var j = 0; j < 7; j++)
                Assert.Equal(210u + (uint)(i * 7 + j), ini.ReadUInt32("Item0", "Animas[" + i + "]" + j));
        Assert.Equal(501u, ini.ReadUInt32("Item0", "BlackJades0"));
        Assert.Equal(502u, ini.ReadUInt32("Item0", "BlackJades1"));
        Assert.Equal(999u, ini.ReadUInt32("Item0", "BlackJades2"));
        Assert.Equal(888u, ini.ReadUInt32("Item0", "BlackJades3"));
        Assert.Equal(7, ini.ReadInt32("BlackJadeBag", "Count", -1));
        Assert.Same(item, Assert.Single(gear.Items).Value);
        Assert.Same(jades, item.BlackJades);
        Assert.Null(jades[2]);
        Assert.Equal(33u, Assert.Single(gear.BlackJadeBag).Value.Data);
    }

    [Fact]
    public void NullBagLoadFaultOccursAfterSavedScalarsAirStateAndWeaponPublicationWithoutRepair()
    {
        using var f = new PersistenceFixture();
        f.Row(201, 1);
        f.Jade(113, 11);
        var ini = f.Ini(3);
        ini.Write("CombatGear", "MasteryPoints", 91);
        ini.Write("CombatGear", "TaoistReiki", 92);
        ini.Write("CombatGear", "PirateExp", 93);
        ini.Write("CombatGear", "Lottery", 94);
        ini.Write("CombatGear", "Count", 1);
        ini.Write("AirPowers", "SenseLevel", 95);
        ini.Write("AirPowers", "OverLoadAipPower", 96);
        ini.Write("Item0", "ItemID", 201);
        ini.Write("Item0", "Level", 1);
        ini.Write("Item0", "Exp", 97);
        ini.Write("BlackJadeBag", "Count", 1);
        ini.Write("BlackJadeBag", "BlackJade0", 113);
        var before = File.ReadAllBytes(f.PathFor(3));
        var gear = new Gear { MasteryPoints = 1, TaoistReiki = 2, PirateExp = 3, Lottery = 4, BlackJadeBag = null! };
        var retained = Weapon(202, 8);
        gear.Items[retained.ItemID] = retained;
        var air = gear.AirPowers;
        air.Id = 98;

        Assert.Throws<NullReferenceException>(() => gear.Load(3));

        Assert.Equal((91u, 92u, 93u, 94u), (gear.MasteryPoints, gear.TaoistReiki, gear.PirateExp, gear.Lottery));
        Assert.Same(air, gear.AirPowers);
        Assert.Equal((98u, 95u, 96u), (air.Id, air.SenseLevel, air.OverLoadAipPower));
        Assert.Equal(2, gear.Items.Count);
        Assert.Same(retained, gear.Items[retained.ItemID]);
        Assert.Equal((1u, 97u), (gear.Items[(Gear.TypeID)201].Level, gear.Items[(Gear.TypeID)201].Exp));
        Assert.Null(gear.BlackJadeBag);
        Assert.Equal(before, File.ReadAllBytes(f.PathFor(3)));
    }

    [Fact]
    public void LateNullDaoqiRowFaultRetainsThePreviouslyLoadedWeaponAndFirstBagAddition()
    {
        using var f = new PersistenceFixture();
        f.Row(201, 1);
        f.Jade(113, 11);
        LegacyNativeDaoqiDictTypeTable.Info[213] = null!;
        var ini = f.Ini(4);
        ini.Write("CombatGear", "MasteryPoints", 101);
        ini.Write("CombatGear", "Count", 1);
        ini.Write("Item0", "ItemID", 201);
        ini.Write("Item0", "Level", 1);
        ini.Write("Item0", "Exp", 102);
        ini.Write("BlackJadeBag", "Count", 2);
        ini.Write("BlackJadeBag", "BlackJade0", 113);
        ini.Write("BlackJadeBag", "BlackJade1", 213);
        var before = File.ReadAllBytes(f.PathFor(4));
        var gear = new Gear { MasteryPoints = 1 };
        var retained = Weapon(202, 8);
        gear.Items[retained.ItemID] = retained;
        var items = gear.Items;
        var bag = gear.BlackJadeBag;
        var air = gear.AirPowers;

        Assert.Throws<NullReferenceException>(() => gear.Load(4));

        Assert.Equal(101u, gear.MasteryPoints);
        Assert.Same(items, gear.Items);
        Assert.Equal(2, items.Count);
        Assert.Same(retained, items[retained.ItemID]);
        Assert.Equal(102u, items[(Gear.TypeID)201].Exp);
        Assert.Same(bag, gear.BlackJadeBag);
        var added = Assert.Single(bag);
        Assert.Equal(113u, added.Key);
        Assert.Equal((113u, 0u), (added.Value.Type, added.Value.Data));
        Assert.False(bag.ContainsKey(213));
        Assert.Same(air, gear.AirPowers);
        Assert.Equal(before, File.ReadAllBytes(f.PathFor(4)));
    }
}
