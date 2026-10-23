using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClanTests
{
    [Fact]
    public void SaveUsesDictionaryKeyForFileNameAndWholeThreeNativeRecords()
    {
        using var state = new State();
        using var files = new Files();
        var clan = Clan(17, "Wolf");
        clan.LeaderName = "Leader";
        clan.Ally[900] = Clan(3, "Ally");
        clan.Enemy[800] = Clan(4, "Enemy");
        LegacyNativeClanRuntime.Clans[77] = clan;
        files.SaveTarget("77.txt");
        LegacyNativeClanTable.Save();
        Assert.False(File.Exists(Path.Combine(files.Clans, "17.txt")));
        Assert.Equal(new[] { "Count=3", "17/Wolf/Leader/1/0/None/0/0/0/0/", "1/3/", "1/4/" },
            File.ReadAllLines(Path.Combine(files.Clans, "77.txt")));
        clan.Donation = 8;
        LegacyNativeClanTable.Save();
        Assert.Equal(4, File.ReadAllLines(Path.Combine(files.Clans, "77.txt")).Length);
        Assert.Contains("/1/8/None/", File.ReadAllText(Path.Combine(files.Clans, "77.txt")));
    }

    [Fact]
    public void LoadBirthsRealClanOwnersResolvesRelationsAndPopulatesNativeMemberRows()
    {
        using var state = new State();
        using var files = new Files();
        files.Clan("a.txt", "20/Wolf/Leader/4/555/Bulletin/3/0/0/0/", "2/30/999/", "1/30/");
        files.Clan("b.txt", "30/Other/OtherLeader/2/777/None/1/0/0/0/", "0/", "0/");
        files.User("member.ini", "UID=17\r\nName=Member\r\nClanID=20\r\nClanRank=100\r\nClass=145\r\nLevel=130\r\nClanDonation=99\r\n");
        files.User("ignored.ini", "UID=18\r\nName=NoClan\r\nClanID=0\r\n");
        files.User("absent.ini", "UID=19\r\nName=AbsentClan\r\nClanID=999\r\n");
        LegacyNativeClanTable.Load();
        var clan = LegacyNativeClanRuntime.Clans[20];
        var other = LegacyNativeClanRuntime.Clans[30];
        Assert.Equal("20/Wolf/Leader/4/555/Bulletin/3/0/0/0/", clan.ToString());
        Assert.Same(other, Assert.Single(clan.Ally).Value);
        Assert.Same(other, Assert.Single(clan.Enemy).Value);
        var member = Assert.Single(clan.Members).Value;
        Assert.Equal(17u, member.UID);
        Assert.Equal("Member", member.Name);
        Assert.Equal(LegacyNativeClanRanks.Leader, member.Rank);
        Assert.Equal(145u, member.Class);
        Assert.Equal((byte)130, member.Level);
        Assert.Equal(99u, member.Donation);
        Assert.False(member.Online);
        Assert.Equal(31u, LegacyNativeClanRuntime.CounterClansID.Count);
        Assert.Equal(32u, LegacyNativeClanRuntime.CounterClansID.Next);
    }

    [Fact]
    public void MemberLoaderRetainsNativeByteParseFallbackLevelWrapAndUndefinedRank()
    {
        using var state = new State();
        using var files = new Files();
        files.Clan("a.txt", "20/Wolf/Leader/1/0/None/0/", "0/", "0/");
        files.User("member.ini", "UID=17\r\nClanID=20\r\nClass=2000\r\nLevel=260\r\n");
        LegacyNativeClanTable.Load();
        var row = LegacyNativeClanRuntime.Clans[20].Members[17];
        Assert.Equal("None", row.Name);
        Assert.Equal((LegacyNativeClanRanks)200, row.Rank);
        Assert.Equal(0u, row.Class);
        Assert.Equal((byte)4, row.Level);
        Assert.Equal(0u, row.Donation);
        Assert.False(row.Online);
    }

    [Fact]
    public void ReloadAccumulatesRawRelationListsButKeepsPreviouslyPublishedClanAndMemberIdentities()
    {
        using var state = new State();
        using var files = new Files();
        files.Clan("a.txt", "20/DiskName/Leader/1/0/None/0/", "2/30/30/", "1/30/");
        files.Clan("b.txt", "30/Other/Leader/1/0/None/0/", "0/", "0/");
        files.User("member.ini", "UID=17\r\nName=DiskMember\r\nClanID=20\r\n");
        var existing = Clan(20, "Existing");
        var member = new LegacyNativeClanMember { UID = 17, Name = "ExistingMember", Online = true };
        existing.Members[17] = member;
        LegacyNativeClanRuntime.Clans[20] = existing;
        LegacyNativeClanTable.Load();
        LegacyNativeClanTable.Load();
        Assert.Same(existing, LegacyNativeClanRuntime.Clans[20]);
        Assert.Equal("Existing", existing.Name);
        Assert.Same(member, existing.Members[17]);
        Assert.True(member.Online);
        Assert.Equal(new uint[] { 30, 30, 30, 30 }, LegacyNativeClanTable.clanally[20]);
        Assert.Equal(new uint[] { 30, 30 }, LegacyNativeClanTable.clanenemy[20]);
        Assert.Single(existing.Ally);
        Assert.Single(existing.Enemy);
    }

    [Fact]
    public void CounterIncrementWrapAtMaximumIdIsPreserved()
    {
        using var state = new State();
        using var files = new Files();
        files.Clan("max.txt", "4294967295/Max/Leader/1/0/None/0/", "0/", "0/");
        LegacyNativeClanTable.Load();
        Assert.Equal(0u, LegacyNativeClanRuntime.CounterClansID.Count);
        Assert.Equal(1u, LegacyNativeClanRuntime.CounterClansID.Next);
        Assert.True(LegacyNativeClanRuntime.Clans.ContainsKey(uint.MaxValue));
    }

    [Fact]
    public void MissingUsersDirectoryFaultsAfterClanPublicationButBeforeRelationResolution()
    {
        using var state = new State();
        using var files = new Files(users: false);
        files.Clan("a.txt", "20/One/Leader/1/0/None/0/", "1/30/", "0/");
        files.Clan("b.txt", "30/Two/Leader/1/0/None/0/", "0/", "0/");
        Assert.Throws<DirectoryNotFoundException>(() => LegacyNativeClanTable.Load());
        Assert.Equal(2, LegacyNativeClanRuntime.Clans.Count);
        Assert.Equal(new uint[] { 30 }, LegacyNativeClanTable.clanally[20]);
        Assert.Empty(LegacyNativeClanRuntime.Clans[20].Ally);
    }

    [Fact]
    public void RelationResolverUsesTargetOwnerIdDoesNotClearAndDoesNotInventReciprocalLinks()
    {
        using var state = new State();
        var owner = Clan(1, "Owner");
        var target = Clan(70, "Target");
        var prior = Clan(5, "Prior");
        LegacyNativeClanRuntime.Clans[1] = owner;
        LegacyNativeClanRuntime.Clans[7] = target;
        owner.Ally[5] = prior;
        LegacyNativeClanTable.clanally[1] = new() { 7, 7, 404 };
        LegacyNativeClanTable.clanally[999] = new() { 7 };
        LegacyNativeClanTable.clanenemy[1] = new() { 7 };
        LegacyNativeClanTable.ClanExecuteAllyAndEnemy();
        Assert.Same(target, owner.Ally[70]);
        Assert.Same(prior, owner.Ally[5]);
        Assert.Same(target, owner.Enemy[70]);
        Assert.Empty(target.Ally);
        Assert.Empty(target.Enemy);
        Assert.Equal(3, LegacyNativeClanTable.clanally[1].Count);
    }

    [Fact]
    public void EmptyClansDirectoryStillLoadsMembersIntoExistingNativeClans()
    {
        using var state = new State();
        using var files = new Files();
        var existing = Clan(20, "Existing");
        LegacyNativeClanRuntime.Clans[20] = existing;
        files.User("member.ini", "UID=17\r\nClanID=20\r\nName=Loaded\r\n");
        LegacyNativeClanTable.Load();
        Assert.Equal("Loaded", existing.Members[17].Name);
        Assert.Equal(10u, LegacyNativeClanRuntime.CounterClansID.Count);
    }
}
