using System.Reflection;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativeNinjaRank;
using Show = WolfConquer.Core.World.LegacyNativeMsgRankMemberShow.MsgRankMemberShowProto;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaRankTests
{
    [Theory]
    [InlineData(0, 37u)]
    [InlineData(1, 39u)]
    [InlineData(2, 38u)]
    [InlineData(3, 40u)]
    [InlineData(4, 43u)]
    [InlineData(5, 114u)]
    [InlineData(6, 122u)]
    [InlineData(7, 154u)]
    [InlineData(8, 42u)]
    [InlineData(9, 41u)]
    [InlineData(10, 151u)]
    [InlineData(11, 152u)]
    [InlineData(12, 153u)]
    [InlineData(13, 167u)]
    [InlineData(14, 166u)]
    [InlineData(15, 35u)]
    [InlineData(16, 34u)]
    [InlineData(17, 203u)]
    [InlineData(18, 232u)]
    public void AddInfoUsesFirstRegisteredFlagInAllNineteenNativePriorityBranches(int first, uint expected)
    {
        using var state = new State();
        var user = new LegacyNativeGameClientRuntime();
        LegacyNativeMsgUpdate.Flags[] flags =
        [
            LegacyNativeMsgUpdate.Flags.WeeklyPKChampion,
            LegacyNativeMsgUpdate.Flags.TopTrojan,
            LegacyNativeMsgUpdate.Flags.TopWarrior,
            LegacyNativeMsgUpdate.Flags.TopArcher,
            LegacyNativeMsgUpdate.Flags.TopNinja,
            LegacyNativeMsgUpdate.Flags.TopMonk,
            LegacyNativeMsgUpdate.Flags.TopPirate,
            LegacyNativeMsgUpdate.Flags.TopDragonLee,
            LegacyNativeMsgUpdate.Flags.TopFireTaoist,
            LegacyNativeMsgUpdate.Flags.TopWaterTaoist,
            LegacyNativeMsgUpdate.Flags.TopSuperGuildWarFiveStars,
            LegacyNativeMsgUpdate.Flags.TopSuperGuildWarThreeStars,
            LegacyNativeMsgUpdate.Flags.TopSuperGuildWarOneStar,
            LegacyNativeMsgUpdate.Flags.TopMrsConquer,
            LegacyNativeMsgUpdate.Flags.TopMrConquer,
            LegacyNativeMsgUpdate.Flags.TopDeputyLeader,
            LegacyNativeMsgUpdate.Flags.TopGuildLeader,
            LegacyNativeMsgUpdate.Flags.TopWindWalker,
            LegacyNativeMsgUpdate.Flags.TopThunderstriker,
        ];
        for (int index = first; index < flags.Length; index++)
            Assert.True(user.Player.BitVector.TryAdd((int)flags[index], 0, false, 0));

        var entry = Entry();
        entry.AddInfo(user);

        Assert.Equal(expected, entry.Flag);
        Assert.Equal(flags.Length - first, user.Player.BitVector.ArrayFlags.Count);
        Assert.Empty(Ranking.Ranks);
    }

    [Fact]
    public void AddInfoReadsCurrentPlayerAndSuppliedUsersEquipmentWithoutRebindingTheirOwners()
    {
        using var state = new State();
        var user = new LegacyNativeGameClientRuntime();
        var originalPlayer = user.Player;
        var foreign = new LegacyNativeGameClientRuntime();
        PopulateRankAppearance(foreign.Player);
        user.Player = foreign.Player;
        user.Equipment = new LegacyNativeEquipItemStorage(user)
        {
            HeadID = 3101, LeftWeapon = 3102, RightWeapon = 3103, ArmorID = uint.MaxValue,
        };
        foreign.Equipment = new LegacyNativeEquipItemStorage(foreign)
        {
            HeadID = 9101, LeftWeapon = 9102, RightWeapon = 9103,
        };
        var guild = LegacyNativeGuildRuntime.CreateLoadedGuild(new string("Current Guild".ToCharArray()));
        foreign.Player.MyGuild = guild;
        foreign.Player.GuildID = uint.MaxValue;
        var entry = Entry();
        var originalName = entry.Name;

        entry.AddInfo(user);

        Assert.Equal(new object[]
        {
            41u, "Hero", 2u, 404u, 505u, (byte)206, 707u,
            3001u, 3101u, 3002u, 3102u, 3004u, 3103u, 3005u,
            3006u, 3003u, 3007u, 17u, 3008u, 2020u, "Current Guild",
        }, Values(entry));
        Assert.Same(originalName, entry.Name); // AddInfo does not copy UID/Name/Class/Level/Mesh/points/type.
        Assert.Same(guild.GuildName, entry.GuildName);
        Assert.Same(foreign, user.Player.Owner);
        Assert.Same(user, originalPlayer.Owner);
        Assert.NotSame(originalPlayer, user.Player);
        Assert.Empty(Ranking.Ranks);
    }

    [Fact]
    public void MissingEquipmentGuildAndFlagsRetainOldFieldsWhileOtherAppearanceFieldsAreOverwritten()
    {
        using var state = new State();
        var user = new LegacyNativeGameClientRuntime();
        var entry = Entry();
        var guildName = entry.GuildName;

        entry.AddInfo(user);

        Assert.Equal(new object[]
        {
            41u, "Hero", 2u, 404u, 505u, (byte)206, 707u,
            0u, 909u, 0u, 1111u, 0u, 1313u, 0u, 0u, 0u, 0u, 0u, 0u, 2020u, "Guild",
        }, Values(entry));
        Assert.Same(guildName, entry.GuildName);
        Assert.Null(user.Equipment);
        Assert.Null(user.Player.MyGuild);

        user.Equipment = new LegacyNativeEquipItemStorage(user);
        var guild = LegacyNativeGuildRuntime.CreateLoadedGuild("Before null");
        guild.GuildName = null!;
        user.Player.MyGuild = guild;
        entry.AddInfo(user);
        Assert.Equal(0u, entry.Head);
        Assert.Equal(0u, entry.LeftWeapon);
        Assert.Equal(0u, entry.RightWeapon);
        Assert.Null(entry.GuildName); // A present guild's physical null is copied, not repaired.
        Assert.Equal(2020u, entry.Flag);
    }

    [Fact]
    public void SpecialWingOverridesOrdinaryWingOnlyWhileItsCurrentPhysicalValueIsNonzero()
    {
        using var state = new State();
        var user = new LegacyNativeGameClientRuntime();
        var entry = Entry();
        user.Player.WingId = 17;
        user.Player.SpecialWingID = uint.MaxValue;
        entry.AddInfo(user);
        Assert.Equal(uint.MaxValue, entry.Wing);
        user.Player.SpecialWingID = 0;
        user.Player.WingId = 19;
        entry.AddInfo(user);
        Assert.Equal(19u, entry.Wing);
        Assert.Equal(0u, user.Player.SpecialWingID);
    }

    [Fact]
    public void FlagSelectionUsesRegistrationsNotTheSeparateBitArray()
    {
        using var state = new State();
        var user = new LegacyNativeGameClientRuntime();
        user.Player.BitVector.Add((int)LegacyNativeMsgUpdate.Flags.WeeklyPKChampion);
        Assert.True(user.Player.BitVector.TryAdd((int)LegacyNativeMsgUpdate.Flags.TopNinja, 0, false, 0));
        user.Player.BitVector.Remove((int)LegacyNativeMsgUpdate.Flags.TopNinja);
        var entry = Entry();

        entry.AddInfo(user);

        Assert.Equal(43u, entry.Flag);
        Assert.True(user.Player.BitVector.Contain((int)LegacyNativeMsgUpdate.Flags.WeeklyPKChampion));
        Assert.False(user.Player.BitVector.Contain((int)LegacyNativeMsgUpdate.Flags.TopNinja));
        Assert.True(user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopNinja));
    }

    [Fact]
    public void NullUserAndNullCurrentPlayerFaultBeforeAnyEntryPublication()
    {
        using var state = new State();
        var entry = Entry();
        var before = Values(entry);
        Assert.Throws<NullReferenceException>(() => entry.AddInfo(null!));
        Assert.Equal(before, Values(entry));
        var user = new LegacyNativeGameClientRuntime { Player = null! };
        Assert.Throws<NullReferenceException>(() => entry.AddInfo(user));
        Assert.Equal(before, Values(entry));
    }

    [Fact]
    public void MissingFlagOwnerFaultsAfterAllAppearanceAndGuildFieldsButBeforeChangingOldFlag()
    {
        using var state = new State();
        var user = new LegacyNativeGameClientRuntime();
        PopulateRankAppearance(user.Player);
        user.Player.SpecialWingID = 4001;
        user.Equipment = new LegacyNativeEquipItemStorage(user)
        {
            HeadID = 3101, LeftWeapon = 3102, RightWeapon = 3103,
        };
        user.Player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild("Published before fault");
        user.Player.BitVector = null!;
        var entry = Entry();

        Assert.Throws<NullReferenceException>(() => entry.AddInfo(user));

        Assert.Equal(new object[]
        {
            41u, "Hero", 2u, 404u, 505u, (byte)206, 707u,
            3001u, 3101u, 3002u, 3102u, 3004u, 3103u, 3005u,
            3006u, 3003u, 4001u, 17u, 3008u, 2020u, "Published before fault",
        }, Values(entry));
        Assert.Same(user.Player.MyGuild.GuildName, entry.GuildName);
        Assert.Empty(Ranking.Ranks);
    }

    [Fact]
    public void ShallowCopyAndNativeConversionKeepEveryPhysicalValueAndStringAliasWithoutMutatingSource()
    {
        using var state = new State();
        var source = Entry();
        source.Name = new string("Hero".ToCharArray());
        source.GuildName = new string("Guild".ToCharArray());
        var before = Values(source);

        var copy = source.ShallowCopy();
        Show.Entry projected = source;
        Show.Entry second = source;

        Assert.NotSame(source, copy);
        Assert.Equal(before, Values(copy));
        Assert.Same(source.Name, copy.Name);
        Assert.Same(source.GuildName, copy.GuildName);
        Assert.NotSame(projected, second);
        Assert.Equal(new object[]
        {
            2u, 1u, 41u, "Hero", "Guild", 707u, 808u, 909u, 1010u, 1111u, 1212u,
            1313u, 1414u, 1515u, 1616u, 1717u, 1818u, 1919u, 2020u, 0u, 0u,
        }, ProjectedRankValues(projected));
        Assert.Equal(ProjectedRankValues(projected), ProjectedRankValues(second));
        Assert.Same(source.Name, projected.Name);
        Assert.Same(source.GuildName, projected.GuildName);
        Assert.Equal(before, Values(source));

        source.UID = uint.MaxValue;
        source.Head = uint.MaxValue;
        source.Name = "Changed";
        source.GuildName = "Changed guild";
        Assert.Equal(41u, copy.UID);
        Assert.Equal(909u, copy.Head);
        Assert.Equal(41u, projected.EntityUID);
        Assert.Equal(909u, projected.Head);
        Assert.Equal("Hero", projected.Name);
        Assert.Equal("Guild", projected.GuildName);
    }

    [Fact]
    public void ConversionCopiesPhysicalNullStringsAndIgnoresSourceRankTypeClassLevelAndScore()
    {
        using var state = new State();
        var source = Entry(uint.MaxValue, uint.MaxValue);
        source.Type = (Ranking.Type)uint.MaxValue;
        source.Class = uint.MaxValue;
        source.Level = byte.MaxValue;
        source.Name = null!;
        source.GuildName = null!;

        Show.Entry projected = source;

        Assert.Null(projected.Name);
        Assert.Null(projected.GuildName);
        Assert.Equal(uint.MaxValue, projected.EntityUID);
        Assert.Equal(2u, projected.Type);
        Assert.Equal(1u, projected.Rank);
        Assert.Equal(0u, projected.Profession);
        Assert.Equal(0u, projected.WeaponWar);
        Assert.Throws<NullReferenceException>(() =>
        {
            _ = (Show.Entry)(Ranking.Entry)null!;
        });
    }

    private static void PopulateRankAppearance(LegacyNativePlayerRuntime player)
    {
        typeof(LegacyNativePlayerRuntime).GetField("_hair", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(player, 3001u);
        player.GarmentId = 3002;
        player.ArmorId = 3003;
        player.LeftWeaponAccessoryId = 3004;
        player.RightWeaponAccessoryId = 3005;
        player.MountArmorId = 3006;
        player.WingId = 3007;
        player.WingPlus = 17;
        player.SpecialTitleID = 3008;
    }

    private static object[] ProjectedRankValues(Show.Entry entry) =>
        [entry.Type, entry.Rank, entry.EntityUID, entry.Name, entry.GuildName, entry.Mesh,
         entry.HairStyle, entry.Head, entry.Garment, entry.LeftWeapon, entry.LefttWeaponAccessory,
         entry.RightWeapon, entry.RightWeaponAccessory, entry.MountArmor, entry.Armor,
         entry.Wing, entry.WingPlus, entry.Title, entry.Flag, entry.Profession, entry.WeaponWar];
}
