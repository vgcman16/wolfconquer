using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDataCoreBirthTests
{
    [Theory]
    [InlineData(0u,"OwnedWolf")] [InlineData(uint.MaxValue,"狼队🐺")]
    public Task WholeCreateCharacteWritesLiteralBase64NameAndConstructsRealOwnersInOwnedUserFile(uint uid,string name)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var files=new Files();files.Seed(uid);Character(scope,name,uid);
        LegacyNativeServerDatabasePersistence.CreateCharacte(scope.Client);var profile=files.Profile(uid);
        Assert.Equal(uid,profile.ReadUInt32("Character","UID"));Assert.Equal(1008u,profile.ReadUInt32("Character","Body"));Assert.Equal(154u,profile.ReadUInt32("Character","Face"));
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes(name)),profile.ReadString("Character","Name","missing"));
        Assert.Equal(7000u,profile.ReadUInt32("Character","Class"));Assert.Equal(1002u,profile.ReadUInt32("Character","Map"));Assert.Equal(410u,profile.ReadUInt32("Character","X"));Assert.Equal(354u,profile.ReadUInt32("Character","Y"));
        Assert.Equal("preserved",profile.ReadString("Other","Sentinel","missing"));Assert.Equal("keep",profile.ReadString("Character","Unrelated","missing"));
        Assert.Same(scope.Client.ArenaStatistic,LegacyNativeArenaRankings.ArenaPoll[uid]);Assert.Same(scope.Client.TeamArenaStatistic,LegacyNativeTeamArenaRankings.ArenaPoll[uid]);
        Assert.Equal(name,scope.Client.ArenaStatistic!.Name);Assert.Equal(uid,scope.Client.ArenaStatistic.UID);Assert.Equal(4000u,scope.Client.ArenaStatistic.Info.ArenaPoints);
        Assert.Equal(name,scope.Client.TeamArenaStatistic!.Name);Assert.Equal(4000u,scope.Client.TeamArenaStatistic.Info.ArenaPoints);
        Assert.Equal(uid,scope.Player.Nobility!.UID);Assert.Equal(name,scope.Player.Nobility.Name);Assert.Equal((uint)1541008,scope.Player.Nobility.Mesh);
        Assert.Equal(uid,scope.Player.Associate!.MyUID);Assert.Same(scope.Client,scope.Player.Associate.MyClient);Assert.Equal(1u,scope.Player.Associate.Online);
        Assert.Equal(name,scope.Player.Flowers!.RedRoses.Name);Assert.Equal(1u,scope.Player.Flowers.FreeFlowers);Assert.Empty(scope.Player.SubClass!.src);
        Assert.Equal(uid,scope.Player.MyChi!.UID);Assert.Equal(4000,scope.Player.MyChi.ChiPoints);Assert.Equal(14,scope.Client.Achievement!.Value.Length);
        Assert.True(scope.Client.FullLoading);Assert.Null(scope.Player.Achievement);Assert.Null(scope.Client.Inventory);Assert.Null(scope.Client.MyMail);
        Assert.Empty(LegacyNativeAssociateRuntime.Associates);Assert.Empty(LegacyNativeFlowersRuntime.ClientPoll);Assert.Empty(LegacyNativeChiRuntime.ChiPool);Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ExistingRankingUidRetainsOriginalGlobalReferencesWhileClientReceivesNewRecords()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var files=new Files();files.User(17);Character(scope);
        var oldSolo=new LegacyNativeArenaUser();oldSolo.Info.ArenaPoints=11;var oldTeam=new LegacyNativeTeamArenaUser();oldTeam.Info.ArenaPoints=12;
        LegacyNativeArenaRankings.ArenaPoll[17]=oldSolo;LegacyNativeTeamArenaRankings.ArenaPoll[17]=oldTeam;
        LegacyNativeServerDatabasePersistence.CreateCharacte(scope.Client);
        Assert.Same(oldSolo,LegacyNativeArenaRankings.ArenaPoll[17]);Assert.Same(oldTeam,LegacyNativeTeamArenaRankings.ArenaPoll[17]);
        Assert.NotSame(oldSolo,scope.Client.ArenaStatistic);Assert.NotSame(oldTeam,scope.Client.TeamArenaStatistic);Assert.Equal(11u,oldSolo.Info.ArenaPoints);Assert.Equal(12u,oldTeam.Info.ArenaPoints);
        Assert.Equal(4000u,scope.Client.ArenaStatistic!.Info.ArenaPoints);Assert.Equal(4000u,scope.Client.TeamArenaStatistic!.Info.ArenaPoints);Assert.True(scope.Client.FullLoading);Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task RepeatedCreateReplacesEveryNativeBirthOwnerButDoesNotRecomposeInventoryOrPlayerAchievement()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var files=new Files();files.User(17);Character(scope);LegacyNativeDataCoreAttributes.LoadClient(scope.Player);
        var members=Members(scope.Client);var runtimeAchievement=scope.Player.Achievement;
        LegacyNativeServerDatabasePersistence.CreateCharacte(scope.Client);var first=CreatedOwners(scope.Client);var firstSolo=scope.Client.ArenaStatistic;var firstTeam=scope.Client.TeamArenaStatistic;
        scope.Player.Name="SecondWolf";LegacyNativeServerDatabasePersistence.CreateCharacte(scope.Client);var second=CreatedOwners(scope.Client);
        for(int i=0;i<first.Length;i++)Assert.NotSame(first[i],second[i]);Assert.Same(firstSolo,LegacyNativeArenaRankings.ArenaPoll[17]);Assert.Same(firstTeam,LegacyNativeTeamArenaRankings.ArenaPoll[17]);
        var current=Members(scope.Client);for(int i=0;i<7;i++)Assert.Same(members[i],current[i]);Assert.Same(runtimeAchievement,scope.Player.Achievement);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("SecondWolf")),files.Profile(17).ReadString("Character","Name","missing"));Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task MissingUsersDirectoryKeepsNativeIgnoredIniWriteFailuresAndContinuesRealOwnerBirth()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var files=new Files(users:false);string path=files.User(17);Character(scope);
        LegacyNativeServerDatabasePersistence.CreateCharacte(scope.Client);
        Assert.False(File.Exists(path));Assert.False(Directory.Exists(files.Users));Assert.True(scope.Client.FullLoading);
        Assert.NotNull(scope.Player.MyChi);Assert.NotNull(scope.Client.Achievement);Assert.Same(scope.Client.ArenaStatistic,LegacyNativeArenaRankings.ArenaPoll[17]);Assert.Empty(scope.Queue);
    });

    private static object[] CreatedOwners(LegacyNativeGameClientRuntime client)=>
        [client.ArenaStatistic!,client.TeamArenaStatistic!,client.Player.Nobility!,client.Player.Associate!,client.Player.Flowers!,client.Player.SubClass!,client.Player.MyChi!,client.Achievement!];
}
