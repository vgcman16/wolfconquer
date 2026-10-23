using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDeathTournamentTests
{
    [Fact]
    public Task ClientOrderingSamplesSingletonKeysAndRetainsNullFaults() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Empty(LegacyNativeDeathTournamentOrdering.KillerElite([])); Assert.Empty(LegacyNativeDeathTournamentOrdering.ExtremePk([]));
        Assert.Same(scope.Client, Assert.Single(LegacyNativeDeathTournamentOrdering.KillerElite([scope.Client])));
        Assert.Same(scope.Client, Assert.Single(LegacyNativeDeathTournamentOrdering.ExtremePk([scope.Client])));
        Assert.Throws<NullReferenceException>(() => LegacyNativeDeathTournamentOrdering.KillerElite([null!]));
        Assert.Throws<NullReferenceException>(() => LegacyNativeDeathTournamentOrdering.ExtremePk([null!]));
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => LegacyNativeDeathTournamentOrdering.KillerElite(null!)).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => LegacyNativeDeathTournamentOrdering.ExtremePk(null!)).ParamName);
    });
    [Fact]
    public Task ClientOrderingPreservesStableTiesUnsignedExtremaAndAliases() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.Player.KillerPkPoints = scope.Client.Player.XtremePkPoints = 2;
        scope.Other.Player.KillerPkPoints = scope.Other.Player.XtremePkPoints = uint.MaxValue;
        var source = new[] { scope.Client, scope.Other, scope.Other, scope.Client };
        var expected = new[] { scope.Other, scope.Other, scope.Client, scope.Client };
        Assert.Equal(expected, LegacyNativeDeathTournamentOrdering.KillerElite(source));
        Assert.Equal(expected, LegacyNativeDeathTournamentOrdering.ExtremePk(source));
        scope.Other.Player.KillerPkPoints = scope.Other.Player.XtremePkPoints = 2;
        Assert.Equal(source, LegacyNativeDeathTournamentOrdering.KillerElite(source));
        Assert.Equal(source, LegacyNativeDeathTournamentOrdering.ExtremePk(source));
    });
    [Fact]
    public Task TeamCountOrderingRetainsLastStableMinimumAndRepeatedAliases() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var blue = new Dictionary<uint, LegacyNativeGameClientRuntime> { [17] = scope.Client };
        var red = new Dictionary<uint, LegacyNativeGameClientRuntime>();
        var black = new Dictionary<uint, LegacyNativeGameClientRuntime>();
        var white = new Dictionary<uint, LegacyNativeGameClientRuntime>();
        var source = new[] { blue, red, black, white };
        Assert.Equal(source, LegacyNativeDeathTournamentOrdering.TeamCounts(source));
        Assert.Same(white, LegacyNativeDeathTournamentOrdering.TeamCounts(source).Last());
        Assert.Equal(new[] { blue, red, red }, LegacyNativeDeathTournamentOrdering.TeamCounts([red, blue, red]));
    });
    [Fact]
    public Task TeamCountOrderingReadsSingletonAndLateNullRatherThanSkipping() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Empty(LegacyNativeDeathTournamentOrdering.TeamCounts([]));
        Assert.Throws<NullReferenceException>(() => LegacyNativeDeathTournamentOrdering.TeamCounts([null!]));
        Assert.Throws<NullReferenceException>(() => LegacyNativeDeathTournamentOrdering.TeamCounts([new(), new(), null!, new()]));
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => LegacyNativeDeathTournamentOrdering.TeamCounts(null!)).ParamName);
    });
}
