using System.Reflection;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativeHWRank;
using Show = WolfConquer.Core.World.LegacyNativeMsgRankMemberShow.MsgRankMemberShowProto;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHWRankTests
{
    [Theory]
    [InlineData(0, 37u)] [InlineData(1, 39u)] [InlineData(2, 38u)] [InlineData(3, 40u)]
    [InlineData(4, 43u)] [InlineData(5, 114u)] [InlineData(6, 122u)] [InlineData(7, 154u)]
    [InlineData(8, 42u)] [InlineData(9, 41u)] [InlineData(10, 35u)] [InlineData(11, 34u)]
    [InlineData(12, 203u)] [InlineData(13, 232u)]
    public void AddInfoRetainsAllFourteenNativeFlagPriorityBranches(int first, uint expected)
    {
        using var state = new State(); var client = new LegacyNativeGameClientRuntime();
        LegacyNativeMsgUpdate.Flags[] flags =
        [
            LegacyNativeMsgUpdate.Flags.WeeklyPKChampion, LegacyNativeMsgUpdate.Flags.TopTrojan,
            LegacyNativeMsgUpdate.Flags.TopWarrior, LegacyNativeMsgUpdate.Flags.TopArcher,
            LegacyNativeMsgUpdate.Flags.TopNinja, LegacyNativeMsgUpdate.Flags.TopMonk,
            LegacyNativeMsgUpdate.Flags.TopPirate, LegacyNativeMsgUpdate.Flags.TopDragonLee,
            LegacyNativeMsgUpdate.Flags.TopFireTaoist, LegacyNativeMsgUpdate.Flags.TopWaterTaoist,
            LegacyNativeMsgUpdate.Flags.TopDeputyLeader, LegacyNativeMsgUpdate.Flags.TopGuildLeader,
            LegacyNativeMsgUpdate.Flags.TopWindWalker, LegacyNativeMsgUpdate.Flags.TopThunderstriker,
        ];
        for (int i = first; i < flags.Length; i++) Assert.True(client.Player.BitVector.TryAdd((int)flags[i], 0, false, 0));
        var entry = Entry(); entry.AddInfo(client);
        Assert.Equal(expected, entry.Flag); Assert.Equal(flags.Length - first, client.Player.BitVector.ArrayFlags.Count);
        Assert.Empty(Ranking.Ranks);
    }

    [Fact]
    public void FiveNinjaOnlyPrioritiesAreIgnoredButBitmapDisagreementCannotAuthorizeAppearance()
    {
        using var state = new State(); var client = new LegacyNativeGameClientRuntime();
        foreach (var flag in new[] { LegacyNativeMsgUpdate.Flags.TopSuperGuildWarFiveStars,
            LegacyNativeMsgUpdate.Flags.TopSuperGuildWarThreeStars, LegacyNativeMsgUpdate.Flags.TopSuperGuildWarOneStar,
            LegacyNativeMsgUpdate.Flags.TopMrsConquer, LegacyNativeMsgUpdate.Flags.TopMrConquer })
            Assert.True(client.Player.BitVector.TryAdd((int)flag, 0, false, 0));
        var entry = Entry(); entry.AddInfo(client); Assert.Equal(2020u, entry.Flag);
        var before = Values(entry);
        client.Player.BitVector.Add((int)LegacyNativeMsgUpdate.Flags.WeeklyPKChampion);
        Assert.Throws<InvalidDataException>(() => entry.AddInfo(client));
        Assert.Equal(before, Values(entry));
        client.Player.BitVector.Remove((int)LegacyNativeMsgUpdate.Flags.WeeklyPKChampion);
        Assert.True(client.Player.BitVector.TryAdd((int)LegacyNativeMsgUpdate.Flags.TopDeputyLeader, 0, false, 0));
        entry.AddInfo(client); Assert.Equal(35u, entry.Flag);
        before = Values(entry);
        client.Player.BitVector.Remove((int)LegacyNativeMsgUpdate.Flags.TopDeputyLeader);
        Assert.Throws<InvalidDataException>(() => entry.AddInfo(client));
        Assert.Equal(before, Values(entry));
    }

    [Fact]
    public void AppearanceUsesCurrentPlayerSuppliedEquipmentGuildAliasAndSpecialWingWithoutRebindingOwners()
    {
        using var state = new State();
        var client = new LegacyNativeGameClientRuntime(); var original = client.Player;
        var foreign = new LegacyNativeGameClientRuntime(); Populate(foreign.Player); client.Player = foreign.Player;
        client.Equipment = new LegacyNativeEquipItemStorage(client) { HeadID = 3101, LeftWeapon = 3102, RightWeapon = 3103 };
        foreign.Equipment = new LegacyNativeEquipItemStorage(foreign) { HeadID = 9901, LeftWeapon = 9902, RightWeapon = 9903 };
        foreign.Player.SpecialWingID = 4001;
        var guild = LegacyNativeGuildRuntime.CreateLoadedGuild(new string("Current Guild".ToCharArray()));
        foreign.Player.MyGuild = guild;
        var entry = Entry(); var name = entry.Name; entry.AddInfo(client);
        Assert.Equal(new object[] { 41u, "Hero", 2u, 404u, 505u, (byte)206, 707u,
            3001u, 3101u, 3002u, 3102u, 3004u, 3103u, 3005u, 3006u, 3003u, 4001u, 17u, 3008u, 2020u, "Current Guild" }, Values(entry));
        Assert.Same(name, entry.Name); Assert.Same(guild.GuildName, entry.GuildName);
        Assert.Same(foreign, client.Player.Owner); Assert.Same(client, original.Owner);
        foreign.Player.SpecialWingID = 0; foreign.Player.WingId = 19; entry.AddInfo(client); Assert.Equal(19u, entry.Wing);
    }

    [Fact]
    public void MissingEquipmentGuildAndFlagsRetainPriorFieldsButPresentNullGuildNameIsCopied()
    {
        using var state = new State(); var client = new LegacyNativeGameClientRuntime(); var entry = Entry();
        entry.AddInfo(client);
        Assert.Equal(new object[] { 41u, "Hero", 2u, 404u, 505u, (byte)206, 707u,
            0u, 909u, 0u, 1111u, 0u, 1313u, 0u, 0u, 0u, 0u, 0u, 0u, 2020u, "Guild" }, Values(entry));
        client.Equipment = new LegacyNativeEquipItemStorage(client);
        client.Player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild("before"); client.Player.MyGuild.GuildName = null!;
        entry.AddInfo(client); Assert.Null(entry.GuildName);
        Assert.Equal(0u, entry.Head); Assert.Equal(0u, entry.LeftWeapon); Assert.Equal(0u, entry.RightWeapon); Assert.Equal(2020u, entry.Flag);
    }

    [Fact]
    public void MissingRequiredFlagOwnerRefusesBeforeAnyAppearanceOrGuildCopy()
    {
        using var state = new State(); var entry = Entry(); var before = Values(entry);
        Assert.Throws<NullReferenceException>(() => entry.AddInfo(null!)); Assert.Equal(before, Values(entry));
        var client = new LegacyNativeGameClientRuntime { Player = null! };
        Assert.Throws<NullReferenceException>(() => entry.AddInfo(client)); Assert.Equal(before, Values(entry));
        client = new LegacyNativeGameClientRuntime(); Populate(client.Player);
        client.Equipment = new LegacyNativeEquipItemStorage(client) { HeadID = 3101, LeftWeapon = 3102, RightWeapon = 3103 };
        client.Player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild("Not published");
        client.Player.BitVector = null!;
        Assert.Throws<InvalidDataException>(() => entry.AddInfo(client));
        Assert.Equal(before, Values(entry)); Assert.Empty(Ranking.Ranks);
    }

    [Fact]
    public void CopyAndConversionKeepFullPhysicalFieldsStringAliasesAndNativeProjectionDefaults()
    {
        using var state = new State(); var source = Entry();
        source.Name = new string("Hero".ToCharArray()); source.GuildName = new string("Guild".ToCharArray());
        var copy = source.ShallowCopy(); Show.Entry projected = source;
        Assert.NotSame(source, copy); Assert.Equal(Values(source), Values(copy));
        Assert.Same(source.Name, copy.Name); Assert.Same(source.GuildName, copy.GuildName);
        Assert.Equal(new object[] { 2u, 1u, 41u, "Hero", "Guild", 707u, 808u, 909u, 1010u,
            1111u, 1212u, 1313u, 1414u, 1515u, 1616u, 1717u, 1818u, 1919u, 2020u, 0u, 0u },
            new object[] { projected.Type, projected.Rank, projected.EntityUID, projected.Name, projected.GuildName,
                projected.Mesh, projected.HairStyle, projected.Head, projected.Garment, projected.LeftWeapon,
                projected.LefttWeaponAccessory, projected.RightWeapon, projected.RightWeaponAccessory, projected.MountArmor,
                projected.Armor, projected.Wing, projected.WingPlus, projected.Title, projected.Flag, projected.Profession, projected.WeaponWar });
        Assert.Same(source.Name, projected.Name); Assert.Same(source.GuildName, projected.GuildName);
        source.UID = uint.MaxValue; source.Type = Ranking.Type.Main; source.Name = null!; source.GuildName = null!;
        Show.Entry nullNames = source; Assert.Null(nullNames.Name); Assert.Null(nullNames.GuildName);
        Assert.Equal(41u, copy.UID); Assert.Equal(41u, projected.EntityUID); Assert.Equal(2u, nullNames.Type); Assert.Equal(1u, nullNames.Rank);
        Assert.Throws<NullReferenceException>(() => { _ = (Show.Entry)(Ranking.Entry)null!; });
    }

    private static void Populate(LegacyNativePlayerRuntime player)
    {
        typeof(LegacyNativePlayerRuntime).GetField("_hair", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(player, 3001u);
        player.GarmentId = 3002; player.ArmorId = 3003; player.LeftWeaponAccessoryId = 3004;
        player.RightWeaponAccessoryId = 3005; player.MountArmorId = 3006; player.WingId = 3007;
        player.WingPlus = 17; player.SpecialTitleID = 3008;
    }
}
