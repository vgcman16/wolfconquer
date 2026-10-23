using System.Reflection;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;
using Show = WolfConquer.Core.World.LegacyNativeMsgRankMemberShow.MsgRankMemberShowProto;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    private static readonly LegacyNativeMsgUpdate.Flags[] RankFlags =
    [
        LegacyNativeMsgUpdate.Flags.WeeklyPKChampion, LegacyNativeMsgUpdate.Flags.TopTrojan,
        LegacyNativeMsgUpdate.Flags.TopWarrior, LegacyNativeMsgUpdate.Flags.TopArcher,
        LegacyNativeMsgUpdate.Flags.TopNinja, LegacyNativeMsgUpdate.Flags.TopMonk,
        LegacyNativeMsgUpdate.Flags.TopPirate, LegacyNativeMsgUpdate.Flags.TopDragonLee,
        LegacyNativeMsgUpdate.Flags.TopFireTaoist, LegacyNativeMsgUpdate.Flags.TopWaterTaoist,
        LegacyNativeMsgUpdate.Flags.TopDeputyLeader, LegacyNativeMsgUpdate.Flags.TopGuildLeader,
        LegacyNativeMsgUpdate.Flags.TopWindWalker, LegacyNativeMsgUpdate.Flags.TopThunderstriker,
    ];

    [Theory]
    [InlineData(0,37u)]
    [InlineData(1,39u)]
    [InlineData(2,38u)]
    [InlineData(3,40u)]
    [InlineData(4,43u)]
    [InlineData(5,114u)]
    [InlineData(6,122u)]
    [InlineData(7,154u)]
    [InlineData(8,42u)]
    [InlineData(9,41u)]
    [InlineData(10,35u)]
    [InlineData(11,34u)]
    [InlineData(12,203u)]
    [InlineData(13,232u)]
    public void AddInfoUsesAllFourteenNativePriorityBranchesNotTheDifferentNinjaPriorityList(int first,uint expected)
    {
        using var state = new State();
        var user = Client();
        for (int index=first;index<RankFlags.Length;index++)
            Assert.True(user.Player.BitVector.TryAdd((int)RankFlags[index],0,false,0));
        var row = Entry();
        row.AddInfo(user);
        Assert.Equal(expected,row.Flag);
        Assert.Empty(Ranking.Ranks);
    }

    [Fact]
    public void AddAndUpdateReadCurrentPhysicalOwnersAndUpdateNeverTouchesFlagsOrPointAliases()
    {
        using var state = new State();
        var user = Client();
        var row = Entry();
        var points = row.Points;
        user.Player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild("Current guild");
        Assert.True(user.Player.BitVector.TryAdd((int)LegacyNativeMsgUpdate.Flags.TopSuperGuildWarFiveStars,0,false,0));
        row.AddInfo(user);
        Assert.Equal(new object[] {41u,"Hero",4u,505u,(byte)206,707u,30004u,
            3001u,3101u,3002u,3102u,3004u,3103u,3005u,3006u,3003u,4001u,17u,3008u,2020u,"Current guild"},Values(row));
        Assert.Same(points,row.Points);
        user.Player.BitVector = null!;
        user.Player.MyGuild = null;
        user.Player.SpecialWingID = 0;
        user.Player.WingId = 88;
        row.UpdateInfo(user);
        Assert.Equal(88u,row.Wing);
        Assert.Equal(2020u,row.Flag);
        Assert.Equal("Current guild",row.GuildName);
        Assert.Same(points,row.Points);
        var before = Values(row);
        Assert.Throws<InvalidDataException>(()=>row.AddInfo(user));
        Assert.Equal(before, Values(row));
        Assert.Equal(2020u,row.Flag);
        Assert.Equal(88u,row.Wing);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingEquipmentFaultsAfterHairBeforeGarmentUnlikeNinjaNullEquipmentBranch(bool update)
    {
        using var state = new State();
        var user = Client();
        user.Equipment = null;
        var row = Entry();
        if (update) Assert.Throws<NullReferenceException>(()=>row.UpdateInfo(user));
        else Assert.Throws<NullReferenceException>(()=>row.AddInfo(user));
        Assert.Equal(3001u,row.HairStyle);
        Assert.Equal(909u,row.Head);
        Assert.Equal(1010u,row.Garment);
        Assert.Equal(2020u,row.Flag);
        Assert.Equal(new uint[] {201,202},row.Points);
    }

    [Fact]
    public void AppearanceUsesSuppliedClientsEquipmentButItsCurrentPlayersAppearanceWithoutRebinding()
    {
        using var state = new State();
        var user = Client();
        var original = user.Player;
        var foreign = Client();
        foreign.Player.GarmentId = 999;
        foreign.Equipment!.HeadID = 444;
        user.Player = foreign.Player;
        var row = Entry();
        row.AddInfo(user);
        Assert.Equal(3101u,row.Head);
        Assert.Equal(999u,row.Garment);
        Assert.Same(foreign,user.Player.Owner);
        Assert.Same(user,original.Owner);
    }

    [Fact]
    public void NativeConversionCopiesAllAppearanceFieldsAndNullStringsButNeverPointsOrClassProjection()
    {
        using var state = new State();
        var row = Entry();
        row.Name = null!;
        row.GuildName = null!;
        Show.Entry projected = row;
        Show.Entry second = row;
        Assert.NotSame(projected,second);
        Assert.Equal(new object?[] {1u,1u,41u,null,null,707u,808u,909u,1010u,1111u,1212u,
            1313u,1414u,1515u,1616u,1717u,1818u,1919u,2020u,0u,0u},
            new object?[] {projected.Type,projected.Rank,projected.EntityUID,projected.Name,projected.GuildName,
                projected.Mesh,projected.HairStyle,projected.Head,projected.Garment,projected.LeftWeapon,
                projected.LefttWeaponAccessory,projected.RightWeapon,projected.RightWeaponAccessory,
                projected.MountArmor,projected.Armor,projected.Wing,projected.WingPlus,projected.Title,
                projected.Flag,projected.Profession,projected.WeaponWar});
        row.Head = 0;
        row.Points![0] = 99;
        Assert.Equal(909u,projected.Head);
        Assert.Throws<NullReferenceException>(()=>{ _=(Show.Entry)(Ranking.Entry)null!; });
    }

    private static LegacyNativeGameClientRuntime Client()
    {
        var user = new LegacyNativeGameClientRuntime();
        user.Equipment = new(user) {HeadID=3101,LeftWeapon=3102,RightWeapon=3103};
        typeof(LegacyNativePlayerRuntime).GetField("_hair",BindingFlags.Instance|BindingFlags.NonPublic)!
            .SetValue(user.Player,3001u);
        user.Player.GarmentId=3002;
        user.Player.ArmorId=3003;
        user.Player.LeftWeaponAccessoryId=3004;
        user.Player.RightWeaponAccessoryId=3005;
        user.Player.MountArmorId=3006;
        user.Player.WingId=3007;
        user.Player.WingPlus=17;
        user.Player.SpecialTitleID=3008;
        user.Player.SpecialWingID=4001;
        return user;
    }
}
