using System.Reflection;
using System.Reflection.Emit;
using WolfConquer.Core.World;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using Ranks = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearEntryTests
{
    [Fact]
    public void ExplicitMyCombatGearConstructionStoresTheUserButDoesNotClaimClientBirth()
    {
        var owner = new LegacyNativeGameClientRuntime();
        Assert.Null(owner.CombatGear);
        var gear = new LegacyNativeMyCombatGear(owner);
        Assert.Same(owner, gear.user);
        Assert.Null(owner.CombatGear);
        Assert.IsAssignableFrom<Gear>(gear);
        Assert.Empty(gear.Items);
        Assert.Empty(gear.BlackJadeBag);
        Assert.Empty(gear.Info);
        Assert.NotNull(gear.AirPowers);
        Assert.Equal(0u, gear.GetClass());
        var second = new LegacyNativeMyCombatGear(owner);
        Assert.NotSame(gear.Items, second.Items);
        Assert.NotSame(gear.AirPowers, second.AirPowers);
        var nullUser = new LegacyNativeMyCombatGear(null!);
        Assert.Null(nullUser.user); // Literal constructor does not validate its user reference.
        Assert.NotNull(nullUser.Items);
        Assert.Throws<NullReferenceException>(() => nullUser.GetEntry());
    }

    [Fact]
    public void NewGetterOnlyMembersReadActualBackingFieldsWithoutCreatingColdOrHotSetters()
    {
        var owner = new LegacyNativeGameClientRuntime();
        var player = owner.Player;
        Assert.Equal(0u, player.Class);
        Assert.Equal((ushort)0, player.Level);
        Assert.Equal(0u, player.Hair);
        Assert.Null(player.MyGuild);
        Assert.Equal(0u, player.SpecialTitleID);
        Assert.Equal(0u, player.SpecialWingID);
        Assert.Equal(0u, player.GarmentId);
        Assert.Equal(0u, player.ArmorId);
        Assert.Equal(0u, player.LeftWeaponAccessoryId);
        Assert.Equal(0u, player.RightWeaponAccessoryId);
        Assert.Equal(0u, player.MountArmorId);
        Assert.Equal(0u, player.WingId);
        Assert.Equal((byte)0, player.WingPlus);
        foreach (var (name, backing, type) in new[]
        {
            ("Class", "_class", typeof(uint)), ("Level", "_level", typeof(ushort)), ("Hair", "_hair", typeof(uint))
        })
        {
            var property = typeof(LegacyNativePlayerRuntime).GetProperty(name)!;
            Assert.Equal(type, property.PropertyType);
            Assert.Null(property.SetMethod);
            var field = PlayerField(backing);
            Assert.True(field.IsPrivate);
            Assert.False(field.IsInitOnly);
            Assert.Equal(type, field.FieldType);
            var member = Assert.Single(Members(property.GetMethod!).Where(instruction => instruction.Member is not null));
            Assert.Equal(OpCodes.Ldfld, member.Op);
            Assert.Equal(field, member.Member);
        }
        owner.FullLoading = true;
        PhysicalPrestate(player, uint.MaxValue, ushort.MaxValue, uint.MaxValue);
        Assert.Equal(uint.MaxValue, player.Class);
        Assert.Equal(ushort.MaxValue, player.Level);
        Assert.Equal(uint.MaxValue, player.Hair);
        Assert.Null(owner.Socket); // Reflective prestate and reads only, not a native hot setter claim.
    }

    [Fact]
    public void DefaultEntryUsesPhysicalDefaultsAndPublishesANewIndependentRankObjectEachTime()
    {
        using var scope = new NativeCombatGearQueryScope(false);
        var owner = new LegacyNativeGameClientRuntime();
        owner.Player.GuildID = 999; // Does not manufacture MyGuild or GuildName.
        var gear = new LegacyNativeMyCombatGear(owner) { Items = null! };
        var first = gear.GetEntry();
        var second = gear.GetEntry();
        Assert.NotSame(first, second);
        Assert.Equal((Ranks.Type)0, first.Type);
        Assert.Equal(0u, first.UID);
        Assert.Same(owner.Player.Name, first.Name);
        Assert.Equal(0u, first.Class);
        Assert.Equal((byte)0, first.Level);
        Assert.Equal(0u, first.Mesh);
        Assert.Equal(0u, first.HairStyle);
        Assert.Equal(0u, first.Garment);
        Assert.Equal(0u, first.Head);
        Assert.Equal(0u, first.LeftWeapon);
        Assert.Equal(0u, first.RightWeapon);
        Assert.Equal(0u, first.LefttWeaponAccessory);
        Assert.Equal(0u, first.RightWeaponAccessory);
        Assert.Equal(0u, first.MountArmor);
        Assert.Equal(0u, first.Wing);
        Assert.Equal(0u, first.WingPlus);
        Assert.Equal(0u, first.Title);
        Assert.Equal(0u, first.Armor);
        Assert.Equal("", first.GuildName);
        Assert.Equal(0u, first.WeaponWar);
        Assert.Equal(0u, first.TotalPoints);
        Assert.Equal(0u, first.Flag);
        first.UID = 44;
        Assert.Equal(0u, second.UID);
        Assert.Null(owner.Equipment);
        Assert.Null(owner.Player.MyGuild);
        Assert.Null(gear.Items); // Disabled metadata reaches the original isEquip gate, not owner repair.
        Assert.Empty(Ranks.Ranks);
    }

    [Fact]
    public void EntryCopiesAllPhysicalValuesAndReadsTheCurrentEquipmentEvenWithAForeignOwner()
    {
        using var scope = new NativeCombatGearQueryScope();
        var owner = PopulatedOwner();
        var foreign = PopulatedOwner(999, 7051);
        var equipment = new LegacyNativeEquipItemStorage(foreign)
        {
            HeadID = 1001, LeftWeapon = 1002, RightWeapon = 1003, ArmorID = 999999
        };
        owner.Equipment = equipment;
        var guild = LegacyNativeGuildRuntime.CreateLoadedGuild(new string("Actual Guild".ToCharArray()));
        owner.Player.MyGuild = guild;
        owner.Player.GuildID = uint.MaxValue;
        var gear = new LegacyNativeMyCombatGear(owner);
        gear.Items[Gear.TypeID.Pirate1] = Equipped(Gear.TypeID.Dragonhowl); // Values, not dictionary key.
        gear.SetClass(7051); // Base private class is NOT the Player.Class used by isOpen/GetEntry.
        var entry = gear.GetEntry();
        Assert.Equal(17u, entry.UID);
        Assert.Same(owner.Player.Name, entry.Name);
        Assert.Equal(2001u, entry.Class);
        Assert.Equal((byte)140, entry.Level);
        Assert.Equal(32011003u, entry.Mesh);
        Assert.Equal(41123u, entry.HairStyle);
        Assert.Equal(902u, entry.Garment);
        Assert.Equal(1001u, entry.Head);
        Assert.Equal(1002u, entry.LeftWeapon);
        Assert.Equal(1003u, entry.RightWeapon);
        Assert.Equal(904u, entry.LefttWeaponAccessory);
        Assert.Equal(905u, entry.RightWeaponAccessory);
        Assert.Equal(906u, entry.MountArmor);
        Assert.Equal(907u, entry.Wing);
        Assert.Equal(17u, entry.WingPlus);
        Assert.Equal(901u, entry.Title);
        Assert.Equal(903u, entry.Armor); // Native uses Player.ArmorId, not Equipment.ArmorID.
        Assert.Same(guild.GuildName, entry.GuildName);
        Assert.Equal((uint)Gear.TypeID.Dragonhowl, entry.WeaponWar);
        Assert.Same(equipment, owner.Equipment);
        Assert.Same(foreign, typeof(LegacyNativeEquipItemStorage)
            .GetField("Owner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(equipment));
        Assert.Equal(7051u, gear.GetClass());
        Assert.Null(owner.CombatGear);
        Assert.Empty(Ranks.Ranks); // GetEntry neither ranks nor publishes its result.
    }

    [Fact]
    public void EntryReadsCurrentUserPlayerReferenceNotThePlayersPhysicalOwner()
    {
        using var scope = new NativeCombatGearQueryScope();
        var user = PopulatedOwner(17);
        var foreign = PopulatedOwner(777, 4001);
        var originalPlayer = user.Player;
        user.Equipment = new LegacyNativeEquipItemStorage(user) { HeadID = 11, LeftWeapon = 12, RightWeapon = 13 };
        foreign.Equipment = new LegacyNativeEquipItemStorage(foreign) { HeadID = 91, LeftWeapon = 92, RightWeapon = 93 };
        var gear = new LegacyNativeMyCombatGear(user);
        gear.Items[Gear.TypeID.StoneCracker] = Equipped(Gear.TypeID.StoneCracker);
        user.Player = foreign.Player; // Explicit reference prestate, not host adoption or owner rebinding.
        var entry = gear.GetEntry();
        Assert.Equal(777u, entry.UID);
        Assert.Equal(4001u, entry.Class);
        Assert.Same(foreign.Player.Name, entry.Name);
        Assert.Equal(11u, entry.Head);
        Assert.Equal(12u, entry.LeftWeapon);
        Assert.Equal(13u, entry.RightWeapon);
        Assert.Equal((uint)Gear.TypeID.StoneCracker, entry.WeaponWar);
        Assert.Same(foreign, user.Player.Owner);
        Assert.Same(user, gear.user);
        Assert.Same(user, originalPlayer.Owner);
        Assert.Same(foreign.Player, user.Player);
        Assert.NotSame(originalPlayer, user.Player);
    }

    [Fact]
    public void MutableUserAndFreshEntrySamplingRetainPreviouslyPublishedValuesAndStringAliases()
    {
        using var scope = new NativeCombatGearQueryScope(false);
        var firstOwner = PopulatedOwner(17);
        var secondOwner = PopulatedOwner(18, 7001);
        firstOwner.Player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild(new string("First".ToCharArray()));
        secondOwner.Player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild(new string("Second".ToCharArray()));
        var gear = new LegacyNativeMyCombatGear(firstOwner);
        var first = gear.GetEntry();
        var originalName = firstOwner.Player.Name;
        var originalGuild = firstOwner.Player.MyGuild.GuildName;
        firstOwner.Player.Name = "Changed later";
        firstOwner.Player.MyGuild.GuildName = "Changed guild";
        firstOwner.Player.GarmentId = 555;
        var updated = gear.GetEntry();
        gear.user = secondOwner;
        var second = gear.GetEntry();
        Assert.NotSame(first, updated);
        Assert.NotSame(updated, second);
        Assert.Same(originalName, first.Name);
        Assert.Same(originalGuild, first.GuildName);
        Assert.Equal(902u, first.Garment);
        Assert.Equal("Changed later", updated.Name);
        Assert.Equal("Changed guild", updated.GuildName);
        Assert.Equal(555u, updated.Garment);
        Assert.Equal(18u, second.UID);
        Assert.Equal(7001u, second.Class);
        Assert.Same(secondOwner.Player.Name, second.Name);
        Assert.Same(secondOwner.Player.MyGuild.GuildName, second.GuildName);
        Assert.Same(secondOwner, gear.user);
        Assert.Null(firstOwner.CombatGear);
        Assert.Null(secondOwner.CombatGear);
    }
}
