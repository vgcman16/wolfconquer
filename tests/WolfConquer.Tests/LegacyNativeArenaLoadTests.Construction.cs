using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeArenaLoadTests
{
    [Fact]
    public Task ArenaConstructorOwnsTheExactMapListCountersAndDistinctLiveDictionaries() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var pool = LegacyNativeArenaRankings.ArenaPoll;
        var top10 = LegacyNativeArenaRankings.Top10;
        var arena = new LegacyNativeArenaRuntime();
        var other = new LegacyNativeArenaRuntime();
        uint[] expectedMaps = { 6011,4020,2351,2352,2353,3820,5040,2057,3825,4000,
            6546,6525,8009,6729,4003,5342,4006,4008,4009,6000,6001,6521,9988,6891,5599,
            8521,1017,1080,1081,2060,6002,6003,601,700,1038,1764,1036,1764,2068,3868 };
        Assert.Equal(expectedMaps, arena.BlockArenaMaps);
        Assert.Equal(LegacyNativeProcesType.Dead, arena.Proces);
        Assert.Empty(arena.Registered); Assert.Empty(arena.MatchesRegistered);
        Assert.Equal(1u, arena.MatchCounter.Count);
        Assert.NotSame(arena.MatchCounter, other.MatchCounter);
        Assert.NotSame(arena.BlockArenaMaps, other.BlockArenaMaps);
        Assert.NotSame(arena.Registered, other.Registered);
        Assert.NotSame(arena.MatchesRegistered, other.MatchesRegistered);
        Assert.True(arena.Registered.TryAdd(999, scope.Client));
        Assert.Same(scope.Client, arena.Registered[999]);
        Assert.Empty(other.Registered); Assert.Equal(2u, arena.MatchCounter.Next);
        Assert.Equal(1u, other.MatchCounter.Count);
        Assert.Same(pool, LegacyNativeArenaRankings.ArenaPoll);
        Assert.Same(top10, LegacyNativeArenaRankings.Top10);
        Assert.Null(LegacyNativeMsgSchedules.Arena);
        Assert.Empty(LegacyNativeMsgSchedules.Tournaments);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public void EntireTournamentInterfaceAndEnumsKeepTheNativeContractWithoutAnImplementationStandIn()
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(LegacyNativeProcesType)));
        Assert.Equal(new byte[] { 1, 2, 3 }, Enum.GetValues<LegacyNativeProcesType>().Select(x => (byte)x));
        var expected = new Dictionary<string, byte> { ["None"]=0,["DragonWar"]=1,["FootBall"]=2,
            ["BattleField"]=3,["DBShower"]=4,["TeamDeathMatch"]=5,["LastManStand"]=6,
            ["ExtremePk"]=7,["KillerOfElite"]=8,["TreasureThief"]=9,["FreezeWar"]=10,
            ["SkillTournament"]=11,["KingOfTheHill"]=12,["Count"]=13,["TopFight"]=14,["QuizShow"]=15,["FindGM"]=16 };
        Assert.Equal(expected.Count, Enum.GetNames<LegacyNativeTournamentType>().Length);
        foreach (var pair in expected) Assert.Equal(pair.Value, (byte)Enum.Parse<LegacyNativeTournamentType>(pair.Key));
        Type contract = typeof(ILegacyNativeTournament);
        Assert.True(contract.IsInterface);
        Assert.Equal(typeof(LegacyNativeProcesType), contract.GetProperty("Process")!.PropertyType);
        Assert.Equal(typeof(LegacyNativeTournamentType), contract.GetProperty("Type")!.PropertyType);
        Assert.True(contract.GetProperty("Process")!.CanWrite); Assert.True(contract.GetProperty("Type")!.CanWrite);
        Assert.Equal(typeof(void), contract.GetMethod("Open")!.ReturnType);
        Assert.Equal(typeof(void), contract.GetMethod("CheckUp")!.ReturnType);
        MethodInfo join = contract.GetMethod("Join")!;
        Assert.Equal(typeof(bool), join.ReturnType);
        Assert.Equal(new[] { typeof(LegacyNativeGameClientRuntime), typeof(LegacyNativePacket) }, join.GetParameters().Select(p => p.ParameterType));
        Assert.Equal(typeof(bool), contract.GetMethod("InTournament")!.ReturnType);
        Assert.Equal(typeof(LegacyNativeGameClientRuntime), Assert.Single(contract.GetMethod("InTournament")!.GetParameters()).ParameterType);
    }

    [Fact]
    public Task SchedulerPrerequisiteHasTheRealDictionaryButDoesNotRunCreateOrAssignArena() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var dictionary = LegacyNativeMsgSchedules.Tournaments;
        _ = new LegacyNativeMsgSchedules(); // Native implicit constructor; no Create invocation.
        Assert.Same(dictionary, LegacyNativeMsgSchedules.Tournaments);
        Assert.Empty(dictionary); Assert.Null(LegacyNativeMsgSchedules.CurrentTournament);
        Assert.Null(LegacyNativeMsgSchedules.Arena);
        Assert.False(typeof(LegacyNativeMsgSchedules).IsAbstract && typeof(LegacyNativeMsgSchedules).IsSealed);
        Assert.Equal(typeof(Dictionary<LegacyNativeTournamentType, ILegacyNativeTournament>),
            typeof(LegacyNativeMsgSchedules).GetField("Tournaments")!.FieldType);
    });

    [Fact]
    public Task WholeMatchConstructorPublishesRealReferencesAndOnlyResetsQualifierStatus() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var first = scope.Client; var second = scope.Peer(18);
        first.ArenaStatistic!.QualifierStatus = LegacyNativeArenaUser.MatchStatus.Winner;
        second.ArenaStatistic!.QualifierStatus = LegacyNativeArenaUser.MatchStatus.Loser;
        first.ArenaStatistic.Damage = 123; second.ArenaStatistic.Cheers = 456;
        DateTime before = DateTime.Now;
        var match = new LegacyNativeArenaMatchRuntime(first, second, 999);
        DateTime after = DateTime.Now;
        Assert.Equal(new[] { first, second }, match.Players);
        Assert.Same(match, first.ArenaMatch); Assert.Same(match, second.ArenaMatch);
        Assert.Equal(999u, match.MatchUID);
        Assert.Equal(LegacyNativeArenaUser.MatchStatus.None, first.ArenaStatistic.QualifierStatus);
        Assert.Equal(LegacyNativeArenaUser.MatchStatus.None, second.ArenaStatistic.QualifierStatus);
        Assert.Equal(123u, first.ArenaStatistic.Damage); Assert.Equal(456u, second.ArenaStatistic.Cheers);
        Assert.Equal(default, match.DoneStamp); Assert.Equal(default, match.FightStamp);
        Assert.InRange(match.StartTimer, before, after);
        Assert.False(match.Done); Assert.False(match.Imported); Assert.Equal(0u, match.dinamicID);
        Assert.Empty(match.Cheerers); Assert.Empty(match.Watchers); Assert.Empty(scope.Queue);
        Assert.Empty(LegacyNativeArenaRankings.ArenaPoll);
    });

    [Fact]
    public Task NullFirstStatisticFaultRetainsBothPublishedMatchesAndRightHandQualifierMutation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var first = scope.Client; var second = scope.Peer(18);
        first.ArenaStatistic = null;
        second.ArenaStatistic!.QualifierStatus = LegacyNativeArenaUser.MatchStatus.Loser;
        Assert.Throws<NullReferenceException>(() => new LegacyNativeArenaMatchRuntime(first, second, 321));
        var partial = Assert.IsType<LegacyNativeArenaMatchRuntime>(first.ArenaMatch);
        Assert.Same(partial, second.ArenaMatch); Assert.Equal(321u, partial.MatchUID);
        Assert.Equal(new[] { first, second }, partial.Players);
        Assert.Equal(LegacyNativeArenaUser.MatchStatus.None, second.ArenaStatistic.QualifierStatus);
        Assert.Equal(default, partial.StartTimer);
        Assert.Empty(partial.Cheerers); Assert.Empty(partial.Watchers);
    });

    [Fact]
    public Task NullSecondStatisticFaultPrecedesEitherQualifierStoreAfterPublishingBothMatches() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var first = scope.Client; var second = scope.Peer(18);
        first.ArenaStatistic!.QualifierStatus = LegacyNativeArenaUser.MatchStatus.Winner;
        second.ArenaStatistic = null;
        Assert.Throws<NullReferenceException>(() => new LegacyNativeArenaMatchRuntime(first, second, 322));
        var partial = Assert.IsType<LegacyNativeArenaMatchRuntime>(first.ArenaMatch);
        Assert.Same(partial, second.ArenaMatch);
        Assert.Equal(LegacyNativeArenaUser.MatchStatus.Winner, first.ArenaStatistic.QualifierStatus);
        Assert.Equal(default, partial.StartTimer);
    });

    [Fact]
    public Task NullFirstClientFaultStillPublishesTheActualPartialMatchToSecondClient() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var second = scope.Peer(18);
        second.ArenaStatistic!.QualifierStatus = LegacyNativeArenaUser.MatchStatus.Loser;
        Assert.Throws<NullReferenceException>(() => new LegacyNativeArenaMatchRuntime(null!, second, 323));
        var partial = Assert.IsType<LegacyNativeArenaMatchRuntime>(second.ArenaMatch);
        Assert.Null(partial.Players[0]); Assert.Same(second, partial.Players[1]);
        Assert.Equal(323u, partial.MatchUID); Assert.Equal(default, partial.StartTimer);
        Assert.Equal(LegacyNativeArenaUser.MatchStatus.Loser, second.ArenaStatistic.QualifierStatus);
    });
}
