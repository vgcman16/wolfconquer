using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;

namespace WolfConquer.Tests;

// Real Win32 INI operations on GUID-owned copies only; never an original save/SQL/host.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeCombatGearPersistenceTests
{
    [Fact]
    public void SaveLoadRoundTripsEveryPersistedScalarArrayAndBagTypeThroughTheNativeIniOwner()
    {
        using var f = new PersistenceFixture();
        var saved = new Gear { MasteryPoints = uint.MaxValue, TaoistReiki = 17, PirateExp = 18, Lottery = 19 };
        saved.AirPowers = new() { Id = 999, SenseLevel = 21, ArmedLevel = 22, OverLoadLevel = 23, Points = 24,
            SenseAipPower = 25, ArmedAipPower = 26, OverLoadAipPower = 27 };
        foreach (var row in new[] { (Id: 201u, Level: 0u), (Id: 202u, Level: 1u), (Id: 203u, Level: 5u) })
        {
            f.Row(row.Id, row.Level > 1 ? row.Level - 1 : row.Level);
            saved.Items[(Gear.TypeID)row.Id] = Weapon(row.Id, row.Level, row.Id);
        }
        f.Jade(113, 11);
        f.Jade(213, 12);
        saved.BlackJadeBag[800] = new(113, 777);
        saved.BlackJadeBag[801] = new(213, 888);
        saved.SetClass(7001);
        saved.Info["not-persisted"] = [123];
        saved.Save(42);

        var loaded = new Gear();
        var air = loaded.AirPowers;
        air.Id = 43;
        loaded.SetClass(999);
        loaded.Info["retained"] = [456];
        loaded.Load(42);

        Assert.True(File.Exists(f.PathFor(42)));
        Assert.Equal(uint.MaxValue, loaded.MasteryPoints);
        Assert.Equal((17u, 18u, 19u), (loaded.TaoistReiki, loaded.PirateExp, loaded.Lottery));
        Assert.Same(air, loaded.AirPowers);
        Assert.Equal((21u, 22u, 23u, 24u, 25u, 26u, 27u), (air.SenseLevel, air.ArmedLevel, air.OverLoadLevel, air.Points,
            air.SenseAipPower, air.ArmedAipPower, air.OverLoadAipPower));
        Assert.Equal(43u, air.Id); // Id, Class and Info are not persisted.
        Assert.Equal(999u, loaded.GetClass());
        Assert.Equal(new[] { "retained" }, loaded.Info.Keys);
        Assert.Equal(3, loaded.Items.Count);
        foreach (var pair in saved.Items) AssertWeapon(pair.Value, loaded.Items[pair.Key]);
        Assert.Equal(new uint[] { 113, 213 }, loaded.BlackJadeBag.Keys.OrderBy(id => id));
        Assert.All(loaded.BlackJadeBag.Values, item => Assert.Equal(0u, item.Data));
        Assert.All(saved.BlackJadeBag.Values, item => Assert.NotEqual(0u, item.Data));
    }

    [Fact]
    public void UnloadedGatePrecedesNullStateAndDoesNotCreateOrReadAnyFile()
    {
        using var f = new PersistenceFixture();
        var gear = new Gear { MasteryPoints = 77, AirPowers = null!, Items = null!, BlackJadeBag = null! };
        LegacyNativeCombatGearTable.m_Load = false;
        gear.Save(1);
        gear.Load(1);
        Assert.Equal(77u, gear.MasteryPoints);
        Assert.Empty(Directory.GetFiles(f.GearDirectory));
    }

    [Fact]
    public void MissingFileDefaultsScalarsButDoesNotClearExistingCollectionsOrCreateAFile()
    {
        using var f = new PersistenceFixture();
        var gear = new Gear { MasteryPoints = 77, TaoistReiki = 78, PirateExp = 79, Lottery = 80 };
        gear.AirPowers.Id = 81;
        gear.AirPowers.Points = 82;
        var item = Weapon(201, 1);
        var jade = new Gear.BlackJade(113, 91);
        gear.Items[item.ItemID] = item;
        gear.BlackJadeBag[113] = jade;
        gear.Load(1);
        Assert.Equal((0u, 0u, 0u, 0u, 0u), (gear.MasteryPoints, gear.TaoistReiki, gear.PirateExp, gear.Lottery, gear.AirPowers.Points));
        Assert.Equal(81u, gear.AirPowers.Id);
        Assert.Same(item, Assert.Single(gear.Items).Value);
        Assert.Same(jade, Assert.Single(gear.BlackJadeBag).Value);
        Assert.False(File.Exists(f.PathFor(1)));
    }

    [Fact]
    public void SaveWithoutParentDirectorySilentlyRetainsNativeWriteFailureRatherThanCreatingDirectories()
    {
        using var f = new PersistenceFixture();
        LegacyNativeServerConfig.DbLocation = Path.Combine(f.Root, "absent") + Path.DirectorySeparatorChar;
        new Gear().Save(1);
        Assert.False(Directory.Exists(Path.Combine(f.Root, "absent")));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("bad")]
    [InlineData("0")]
    public void NonPositiveOrMalformedCountsSkipRowsWithoutClearingExistingValues(string count)
    {
        using var f = new PersistenceFixture();
        var ini = f.Ini(1);
        ini.WriteString("CombatGear", "Count", count);
        ini.WriteString("BlackJadeBag", "Count", count);
        ini.Write("Item0", "ItemID", 201);
        var gear = new Gear();
        var retained = Weapon(202, 1);
        gear.Items[retained.ItemID] = retained;
        gear.BlackJadeBag[9] = new(9, 99);
        LegacyNativeCombatGearTable.Info = null!;
        LegacyNativeDaoqiDictTypeTable.Info = null!;
        gear.Load(1);
        Assert.Same(retained, Assert.Single(gear.Items).Value);
        Assert.Equal(99u, Assert.Single(gear.BlackJadeBag).Value.Data);
    }

    [Fact]
    public void NativeTruncatedNumbersAndParseFailuresApplyDefaultsBeforeFollowingValidValues()
    {
        using var f = new PersistenceFixture();
        var ini = f.Ini(1);
        ini.WriteString("CombatGear", "MasteryPoints", "000004294967295ignored"); // First14 chars=>429496729.
        ini.WriteString("CombatGear", "TaoistReiki", "-1");
        ini.WriteString("CombatGear", "PirateExp", "4294967296");
        ini.WriteString("CombatGear", "Lottery", "4294967295");
        var gear = new Gear();
        gear.Load(1);
        Assert.Equal(429496729u, gear.MasteryPoints);
        Assert.Equal(0u, gear.TaoistReiki);
        Assert.Equal(0u, gear.PirateExp);
        Assert.Equal(uint.MaxValue, gear.Lottery);
    }

    [Fact]
    public void DuplicateAndRepeatedRowsKeepExistingItemsWhileMissingMetadataRowsAreSkipped()
    {
        using var f = new PersistenceFixture();
        var ini = f.Ini(1);
        ini.Write("CombatGear", "Count", 3);
        for (int i = 0; i < 3; i++)
        {
            ini.Write("Item" + i, "ItemID", i == 2 ? 202 : 201);
            ini.Write("Item" + i, "Level", 5);
            ini.Write("Item" + i, "Exp", 100 + i);
        }
        f.Row(201, 4);
        var gear = new Gear();
        gear.Load(1);
        var item = Assert.Single(gear.Items).Value;
        Assert.Equal(100u, item.Exp);
        gear.Load(1);
        Assert.Same(item, Assert.Single(gear.Items).Value);
        Assert.Equal(100u, item.Exp);
    }

    [Fact]
    public void MetadataLookupStillPrecedesDuplicateContainmentAndLateFailureRetainsEarlierRows()
    {
        using var f = new PersistenceFixture();
        var ini = f.Ini(1);
        ini.Write("CombatGear", "MasteryPoints", 99);
        ini.Write("CombatGear", "Count", 2);
        ini.Write("Item0", "ItemID", 201);
        ini.Write("Item0", "Level", 1);
        ini.Write("Item1", "ItemID", 202);
        ini.Write("Item1", "Level", 1);
        f.Row(201, 1);
        LegacyNativeCombatGearTable.Info[202] = null!;
        var gear = new Gear();
        var existing = Weapon(202, 8);
        gear.Items[existing.ItemID] = existing;
        Assert.Throws<NullReferenceException>(() => gear.Load(1));
        Assert.Equal(99u, gear.MasteryPoints);
        Assert.Equal(2, gear.Items.Count);
        Assert.Equal(1u, gear.Items[(Gear.TypeID)201].Level);
        Assert.Same(existing, gear.Items[(Gear.TypeID)202]);
    }

    [Fact]
    public void BagLoadingUsesCurrentMetadataDedupAndDiscardsSavedDataWhileKeepingPreexistingAliases()
    {
        using var f = new PersistenceFixture();
        f.Jade(113, 11);
        f.Jade(213, 11);
        f.Jade(313, 12);
        var ini = f.Ini(1);
        ini.Write("BlackJadeBag", "Count", 4);
        foreach (var pair in new[] { (0, 213u), (1, 999u), (2, 313u), (3, 313u) })
            ini.Write("BlackJadeBag", "BlackJade" + pair.Item1, pair.Item2);
        var gear = new Gear();
        var retained = new Gear.BlackJade(113, 77);
        gear.BlackJadeBag[800] = retained;
        gear.Load(1);
        Assert.Same(retained, gear.BlackJadeBag[800]);
        Assert.Equal(77u, retained.Data);
        Assert.Equal(313u, gear.BlackJadeBag[313].Type);
        Assert.Equal(0u, gear.BlackJadeBag[313].Data);
        Assert.Equal(2, gear.BlackJadeBag.Count);
    }

    [Fact]
    public void NullAirOwnerFaultOccursAfterFourLoadedOrSavedScalarsAndBeforeTheRemainingFileSections()
    {
        using var f = new PersistenceFixture();
        var ini = f.Ini(1);
        ini.Write("CombatGear", "MasteryPoints", 10);
        ini.Write("CombatGear", "TaoistReiki", 11);
        ini.Write("CombatGear", "PirateExp", 12);
        ini.Write("CombatGear", "Lottery", 13);
        var gear = new Gear { AirPowers = null! };
        Assert.Throws<NullReferenceException>(() => gear.Load(1));
        Assert.Equal((10u, 11u, 12u, 13u), (gear.MasteryPoints, gear.TaoistReiki, gear.PirateExp, gear.Lottery));
        gear.MasteryPoints = 20;
        Assert.Throws<NullReferenceException>(() => gear.Save(2));
        Assert.Equal(20u, f.Ini(2).ReadUInt32("CombatGear", "MasteryPoints"));
        Assert.Equal(0, f.Ini(2).ReadInt32("CombatGear", "Count", -1));
        Assert.Equal(-1, f.Ini(2).ReadInt32("AirPowers", "SenseLevel", -1));
    }

    [Fact]
    public void SaveRetainsStaleRowsButCountGovernsTheNextLoad()
    {
        using var f = new PersistenceFixture();
        var gear = new Gear();
        gear.Items[(Gear.TypeID)201] = Weapon(201, 1);
        gear.BlackJadeBag[113] = new(113, 5);
        gear.Save(1);
        gear.Items.Clear();
        gear.BlackJadeBag.Clear();
        gear.Save(1);
        var ini = f.Ini(1);
        Assert.Equal(201u, ini.ReadUInt32("Item0", "ItemID"));
        Assert.Equal(113u, ini.ReadUInt32("BlackJadeBag", "BlackJade0"));
        Assert.Equal(0, ini.ReadInt32("CombatGear", "Count", -1));
        Assert.Equal(0, ini.ReadInt32("BlackJadeBag", "Count", -1));
        LegacyNativeCombatGearTable.Info = null!;
        LegacyNativeDaoqiDictTypeTable.Info = null!;
        var loaded = new Gear();
        loaded.Load(1);
        Assert.Empty(loaded.Items);
        Assert.Empty(loaded.BlackJadeBag);
    }
}
