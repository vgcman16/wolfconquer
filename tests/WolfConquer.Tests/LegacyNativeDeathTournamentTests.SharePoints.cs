using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDeathTournamentTests
{
    [Theory]
    [InlineData(0u, 10u, 0u, 10u)]
    [InlineData(4u, 10u, 4u, 10u)]
    [InlineData(19u, 10u, 16u, 13u)]
    [InlineData(1000u, 10u, 800u, 210u)]
    [InlineData(10u, 4294967295u, 8u, 1u)]
    public Task SharePointsTransfersFifthThenPublishesBothMessages(uint victim, uint killer, uint left, uint gained) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Client.Player.XtremePkPoints = victim; scope.Other.Player.XtremePkPoints = killer;
        // Native has no tournament/map/alive gate on this complete method.
        scope.Other.Player.Map = 1002;
        var owner = new LegacyNativeMsgExtremePk((LegacyNativeTournamentType)0);
        owner.SharePoints(scope.Client, scope.Other);
        Assert.Equal(left, scope.Client.Player.XtremePkPoints); Assert.Equal(gained, scope.Other.Player.XtremePkPoints);
        Assert.Equal("You gained " + victim / 5 + " points for killing Victim", Text(Assert.Single(scope.OtherQueue)));
        Assert.Equal("You lost " + victim / 5 + " points when Killer has killed you.", Text(Assert.Single(scope.Queue)));
        Assert.Equal((uint)LegacyNativeMsgMessage.ChatMode.Center, Chat(scope.Queue.Peek()).ChatType);
    });
    [Fact]
    public Task SharePointsSelfTransferRetainsBalanceAndTwoActualMessages() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Client.Player.XtremePkPoints = uint.MaxValue;
        new LegacyNativeMsgExtremePk((LegacyNativeTournamentType)0).SharePoints(scope.Client, scope.Client);
        Assert.Equal(uint.MaxValue, scope.Client.Player.XtremePkPoints);
        Assert.Equal(2, scope.Queue.Count); Assert.StartsWith("You gained ", Text(scope.Queue.Dequeue())); Assert.StartsWith("You lost ", Text(scope.Queue.Dequeue()));
    });
    [Fact]
    public Task SharePointsNullKillerFaultsBeforeVictimDeduction() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Client.Player.XtremePkPoints = 100;
        Assert.Throws<NullReferenceException>(() => new LegacyNativeMsgExtremePk((LegacyNativeTournamentType)0).SharePoints(scope.Client, null!));
        Assert.Equal(100u, scope.Client.Player.XtremePkPoints); Assert.Empty(scope.Queue);
    });
    [Fact]
    public Task SharePointsFirstAllocationFaultRetainsBothBalanceCommits() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Client.Player.XtremePkPoints = 100; scope.Other.Player.XtremePkPoints = 8;
        scope.FailNextTake();
        Assert.Throws<NullReferenceException>(() => new LegacyNativeMsgExtremePk((LegacyNativeTournamentType)0).SharePoints(scope.Client, scope.Other));
        Assert.Equal(80u, scope.Client.Player.XtremePkPoints); Assert.Equal(28u, scope.Other.Player.XtremePkPoints);
        Assert.Empty(scope.Queue); Assert.Empty(scope.OtherQueue);
    });
    [Fact]
    public Task SharePointsSecondAllocationFaultRetainsFirstPublication() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Client.Player.XtremePkPoints = 100; scope.Other.Player.XtremePkPoints = 8;
        scope.FailSecondTake();
        Assert.Throws<NullReferenceException>(() => new LegacyNativeMsgExtremePk((LegacyNativeTournamentType)0).SharePoints(scope.Client, scope.Other));
        Assert.Equal(80u, scope.Client.Player.XtremePkPoints); Assert.Equal(28u, scope.Other.Player.XtremePkPoints);
        Assert.Equal("You gained 20 points for killing Victim", Text(Assert.Single(scope.OtherQueue))); Assert.Empty(scope.Queue);
    });
    [Fact]
    public Task SharePointsNullKillerNameFaultsAfterFirstMessageAndBothBalances() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Client.Player.XtremePkPoints = 100; scope.Other.Player.Name = null!;
        Assert.Throws<NullReferenceException>(() => new LegacyNativeMsgExtremePk((LegacyNativeTournamentType)0).SharePoints(scope.Client, scope.Other));
        Assert.Equal(80u, scope.Client.Player.XtremePkPoints); Assert.Equal(20u, scope.Other.Player.XtremePkPoints);
        Assert.Single(scope.OtherQueue); Assert.Empty(scope.Queue);
    });
}
