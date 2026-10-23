using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDataCoreBirthTests
{
    [Fact]
    public Task NullCharacterNameFaultsBeforeFirstIniWriteOrRecordBirth()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var files=new Files();string path=files.User(17);Character(scope);scope.Player.Name=null!;
        Assert.Throws<ArgumentNullException>(()=>LegacyNativeServerDatabasePersistence.CreateCharacte(scope.Client));
        Assert.False(File.Exists(path));Assert.Null(scope.Client.ArenaStatistic);Assert.Null(scope.Client.TeamArenaStatistic);
        Assert.Null(scope.Player.Nobility);Assert.Null(scope.Player.Associate);Assert.Null(scope.Client.Achievement);Assert.False(scope.Client.FullLoading);
        Assert.Empty(LegacyNativeArenaRankings.ArenaPoll);Assert.Empty(LegacyNativeTeamArenaRankings.ArenaPoll);Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task NullSoloPoolFaultsAfterCompleteIniAndSoloRecordButBeforeNobilityTeamOrLaterOwners()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var files=new Files();files.User(17);Character(scope);LegacyNativeArenaRankings.ArenaPoll=null!;
        Assert.Throws<NullReferenceException>(()=>LegacyNativeServerDatabasePersistence.CreateCharacte(scope.Client));
        var profile=files.Profile(17);Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("OwnedWolf")),profile.ReadString("Character","Name","missing"));Assert.Equal(354u,profile.ReadUInt32("Character","Y"));
        Assert.NotNull(scope.Client.ArenaStatistic);Assert.Equal(4000u,scope.Client.ArenaStatistic!.Info.ArenaPoints);Assert.Equal(17u,scope.Client.ArenaStatistic.UID);
        Assert.Null(scope.Player.Nobility);Assert.Null(scope.Client.TeamArenaStatistic);Assert.Null(scope.Player.Associate);Assert.Null(scope.Player.MyChi);
        Assert.Null(scope.Client.Achievement);Assert.False(scope.Client.FullLoading);Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task NullTeamPoolFaultsAfterSoloPublicationNobilityAndTeamRecordWithoutReplacingLaterAliases()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var files=new Files();files.User(17);Character(scope);
        var associate=new LegacyNativeAssociateRuntime.MyAsociats(77);scope.Player.Associate=associate;
        var flowers=new LegacyNativeFlowersRuntime(77,"Held");scope.Player.Flowers=flowers;
        var achievement=new LegacyNativeAchievementCollection{Key=71};scope.Client.Achievement=achievement;
        LegacyNativeTeamArenaRankings.ArenaPoll=null!;
        Assert.Throws<NullReferenceException>(()=>LegacyNativeServerDatabasePersistence.CreateCharacte(scope.Client));
        Assert.Same(scope.Client.ArenaStatistic,LegacyNativeArenaRankings.ArenaPoll[17]);Assert.Equal(17u,scope.Player.Nobility!.UID);
        Assert.Equal(4000u,scope.Client.TeamArenaStatistic!.Info.ArenaPoints);Assert.Equal(354u,files.Profile(17).ReadUInt32("Character","Y"));
        Assert.Same(associate,scope.Player.Associate);Assert.Same(flowers,scope.Player.Flowers);Assert.Same(achievement,scope.Client.Achievement);
        Assert.Null(scope.Player.SubClass);Assert.Null(scope.Player.MyChi);Assert.False(scope.Client.FullLoading);Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task NullCreateClientFaultsBeforeOwnedFileOrCatalogMutation()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var files=new Files();string path=files.User(17);
        Assert.Throws<NullReferenceException>(()=>LegacyNativeServerDatabasePersistence.CreateCharacte(null!));
        Assert.False(File.Exists(path));Assert.Empty(LegacyNativeArenaRankings.ArenaPoll);Assert.Empty(LegacyNativeTeamArenaRankings.ArenaPoll);Assert.Empty(scope.Queue);
    });
}
