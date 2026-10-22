using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using Ranks = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearEntryTests
{
    [Fact]
    public void WrapperEvaluatesGetEntryBeforeTheUnloadedBaseGuard()
    {
        using var scope = new NativeCombatGearQueryScope(false);
        var gear = new LegacyNativeMyCombatGear(null!);
        ((Gear)gear).UpdateRank(null!); // The genuine base method guards before dereferencing its entry.
        Assert.Throws<NullReferenceException>(() => gear.UpdateRank());
        Assert.Empty(Ranks.Ranks);
        var owner = PopulatedOwner();
        var validOwner = new LegacyNativeMyCombatGear(owner) { Items = null! };
        validOwner.UpdateRank(); // GetEntry's own isOpen gate suppresses null Items when unloaded.
        Assert.Null(validOwner.Items);
        Assert.Null(owner.CombatGear);
        Assert.Empty(Ranks.Ranks);
    }

    [Fact]
    public void LoadedMetadataFaultOccursBeforeRankPublicationWithoutAnUnloadedMetadataFallback()
    {
        using var scope = new NativeCombatGearQueryScope();
        Ranks.Create();
        var owner = PopulatedOwner(classId: 7001);
        var gear = new LegacyNativeMyCombatGear(owner);
        gear.Items[Gear.TypeID.Pirate1] = new Gear.Weapon { ItemID = Gear.TypeID.Pirate1, Level = 1, Equip = 1 };
        LegacyNativeCombatGearTable.Info = null!;
        LegacyNativeCombatGearTable.m_Load = false;
        gear.UpdateRank(); // No cache access is invented while both native gates are disabled.
        Assert.All(Ranks.Ranks.Values, rank => Assert.Empty(rank.RankingList));
        LegacyNativeCombatGearTable.m_Load = true;
        Assert.Equal(701u, gear.GetEntry().WeaponWar); // GetEntry itself needs no CQ row.
        Assert.Throws<NullReferenceException>(() => gear.UpdateRank()); // Pirate score reads the real absent cache.
        Assert.All(Ranks.Ranks.Values, rank => Assert.Empty(rank.RankingList));
        Assert.Null(LegacyNativeCombatGearTable.Info);
        Assert.Null(owner.CombatGear);
    }

    [Fact]
    public void WrapperPublishesCurrentPhysicalEntryCopiesWithNativeScoresWithoutChangingStoredClassOrBirth()
    {
        using var scope = new NativeCombatGearQueryScope();
        Ranks.Create();
        var owner = PopulatedOwner();
        owner.Equipment = new LegacyNativeEquipItemStorage(owner) { HeadID = 101, LeftWeapon = 102, RightWeapon = 103 };
        var guildName = new string("Physical Guild".ToCharArray());
        owner.Player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild(guildName);
        owner.Player.SpecialWingID = 888;
        var gear = new LegacyNativeMyCombatGear(owner);
        gear.SetClass(7001); // Not the authority: Player.Class is physically 2001.
        var first = new Gear.Weapon { ItemID = Gear.TypeID.Dragonhowl, Equip = 1, Exp = 10, Transform = 2 };
        gear.Items[Gear.TypeID.Dragonhowl] = first;
        gear.Items[Gear.TypeID.Bloodlust] = new Gear.Weapon { ItemID = Gear.TypeID.Bloodlust, Exp = 20, Transform = 3 };
        var name = owner.Player.Name;

        gear.UpdateRank();

        var entries = Ranks.Ranks.Values.SelectMany(rank => rank.RankingList.Values).ToArray();
        Assert.Equal(4, entries.Length);
        Assert.Equal(4, entries.Distinct().Count());
        Assert.All(entries, entry =>
        {
            Assert.Equal(17u, entry.UID);
            Assert.Same(name, entry.Name);
            Assert.Equal((byte)140, entry.Level);
            Assert.Equal(2001u, entry.Class);
            Assert.Equal(32011003u, entry.Mesh);
            Assert.Equal(41123u, entry.HairStyle);
            Assert.Equal(902u, entry.Garment);
            Assert.Equal(101u, entry.Head);
            Assert.Equal(102u, entry.LeftWeapon);
            Assert.Equal(103u, entry.RightWeapon);
            Assert.Equal(905u, entry.RightWeaponAccessory);
            Assert.Equal(904u, entry.LefttWeaponAccessory);
            Assert.Equal(906u, entry.MountArmor);
            Assert.Equal(888u, entry.Wing);
            Assert.Equal(17u, entry.WingPlus);
            Assert.Equal(901u, entry.Title);
            Assert.Equal(903u, entry.Armor);
            Assert.Same(guildName, entry.GuildName);
            Assert.Equal(201u, entry.WeaponWar);
        });
        var overall = Ranks.GetInfo(Ranks.Type.Overall_Warrior, 17);
        Assert.Equal(250030u, overall.TotalPoints);
        Assert.Equal(10u, Ranks.GetInfo(Ranks.Type.Dragonhowl, 17).TotalPoints);
        Assert.Equal(20u, Ranks.GetInfo(Ranks.Type.Bloodlust, 17).TotalPoints);
        Assert.Equal(0u, Ranks.GetInfo(Ranks.Type.Redcurse, 17).TotalPoints);
        first.Exp = 11;
        owner.Player.Name = "Changed Physical Hero";
        gear.UpdateRank();
        var replacement = Ranks.GetInfo(Ranks.Type.Overall_Warrior, 17);
        Assert.NotSame(overall, replacement);
        Assert.Equal(250031u, replacement.TotalPoints);
        Assert.Equal("Changed Physical Hero", replacement.Name);
        Assert.Same(name, overall.Name);
        Assert.Equal(250030u, overall.TotalPoints);
        Assert.Equal(7001u, gear.GetClass());
        Assert.Equal(2001u, owner.Player.Class);
        Assert.Null(owner.CombatGear);
        Assert.Null(owner.MySpells);
        Assert.Null(owner.Socket);
    }
}
