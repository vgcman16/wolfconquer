using WolfConquer.Core.World;
using Leaves = WolfConquer.Core.Data.LegacyNativeServerDatabasePersistence;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeQueuedPersistenceTests
{
    [WindowsNativeIniTheory]
    [InlineData(0u)]
    [InlineData(uint.MaxValue)]
    public void MemberOverloadOnlyResetsRankAtExactUintUidPathAndDoesNotMutateMember(uint uid)
    {
        using var files = new Files();
        string path = files.User(uid);
        files.Seed(path);
        var member = new LegacyNativeGuildRuntime.Member
        {
            UID = uid, Name = "Physical", Rank = (LegacyNativeGuildMemberRank)ushort.MaxValue,
            Level = 123, Class = 404, MoneyDonate = -999, CpsDonate = uint.MaxValue,
        };

        Leaves.UpdateGuildMember(member);

        Assert.Equal(UserRows.Replace("GuildRank=6\r\n", "GuildRank=0\r\n", StringComparison.Ordinal),
            File.ReadAllText(path));
        Assert.Equal((ushort)0, files.Profile(path).ReadUInt16("Character", "GuildRank", 999));
        Assert.Equal(444u, files.Profile(path).ReadUInt32("Character", "GuildID"));
        Assert.Equal(uid, member.UID);
        Assert.Equal("Physical", member.Name);
        Assert.Equal((LegacyNativeGuildMemberRank)ushort.MaxValue, member.Rank);
        Assert.Equal(123u, member.Level);
        Assert.Equal(404u, member.Class);
        Assert.Equal(-999L, member.MoneyDonate);
        Assert.Equal(uint.MaxValue, member.CpsDonate);
    }

    [WindowsNativeIniFact]
    public void UpdateDbOverloadResetsRankAndGuildIdButNotItsPhysicalQueuedValues()
    {
        using var files = new Files();
        string path = files.User(17);
        files.Seed(path);
        var update = new LegacyNativeGuildRuntime.UpdateDB
        {
            UID = 17, GuildID = uint.MaxValue, Rank = LegacyNativeGuildMemberRank.GuildLeader,
        };

        Leaves.UpdateGuildMember(update);

        Assert.Equal(UserRows.Replace("GuildRank=6\r\n", "GuildRank=0\r\n", StringComparison.Ordinal)
            .Replace("GuildID=444\r\n", "GuildID=0\r\n", StringComparison.Ordinal), File.ReadAllText(path));
        Assert.Equal((ushort)0, files.Profile(path).ReadUInt16("Character", "GuildRank", 999));
        Assert.Equal((ushort)0, files.Profile(path).ReadUInt16("Character", "GuildID", 999));
        Assert.Equal(17u, update.UID);
        Assert.Equal(uint.MaxValue, update.GuildID);
        Assert.Equal(LegacyNativeGuildMemberRank.GuildLeader, update.Rank);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullGuildMemberFaultsBeforeAnyIniFileIsCreated(bool update)
    {
        using var files = new Files();
        if (update)
            Assert.Throws<NullReferenceException>(() => Leaves.UpdateGuildMember((LegacyNativeGuildRuntime.UpdateDB)null!));
        else
            Assert.Throws<NullReferenceException>(() => Leaves.UpdateGuildMember((LegacyNativeGuildRuntime.Member)null!));
        Assert.Empty(Directory.GetFileSystemEntries(files.Users));
    }

    [WindowsNativeIniTheory]
    [InlineData(1002, 0u)]
    [InlineData(-1, uint.MaxValue)]
    public void MapRaceWritesCurrentUintRecordAtPhysicalMapIdAndPreservesOtherRows(int id, uint record)
    {
        using var files = new Files();
        var map = new LegacyNativeGameMap(2, 3, id) { RecordSteedRace = record };
        var cells = map.cells;
        string path = files.Map(unchecked((uint)id));
        const string before = "; map sentinel\r\n[info]\r\nrace_record=12\r\nname=Exact map\r\n" +
            "[other]\r\nrace_record=88\r\n";
        files.Seed(path, before);

        Leaves.UpdateMapRace(map);

        Assert.Equal(before.Replace("race_record=12\r\n", "race_record=" + record + "\r\n",
            StringComparison.Ordinal), File.ReadAllText(path));
        Assert.Equal(record, files.Profile(path).ReadUInt32("info", "race_record", 123));
        Assert.Equal(unchecked((uint)id), map.ID);
        Assert.Equal(record, map.RecordSteedRace);
        Assert.Same(cells, map.cells);
        Assert.Null(map.View);
    }

    [Fact]
    public void NullMapFaultsBeforeAnyIniFileIsCreated()
    {
        using var files = new Files();
        Assert.Throws<NullReferenceException>(() => Leaves.UpdateMapRace(null!));
        Assert.Empty(Directory.GetFileSystemEntries(files.Maps));
    }
}
