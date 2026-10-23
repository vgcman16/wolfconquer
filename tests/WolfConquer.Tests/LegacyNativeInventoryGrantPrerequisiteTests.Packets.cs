using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryGrantPrerequisiteTests
{
    [Theory]
    [InlineData(0u)] [InlineData(1u)] [InlineData(2u)] [InlineData(uint.MaxValue)]
    public Task BothInfoWritersPreserveEveryLiteralWordAndUnknownAction(uint action) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var arena = new LegacyNativeMsgArenaInfo { Length = 999, PacketID = 888, TodayRank = 1,
            Status = (LegacyNativeMsgArenaInfo.Action)action, TotalWin = 2, TotalLose = 3,
            TodayWin = 4, TodayBattles = 5, HistoryHonor = 6, CurrentHonor = 7, ArenaPoints = uint.MaxValue };
        scope.Native.Packet.ArenaInfoCreate(arena);
        Assert.Equal(Words(2191, 1, 0, action, 2, 3, 4, 5, 6, 7, uint.MaxValue, 0, 0),
            NativeSpellTestFixture.Bytes(scope.Native.Packet));
        Assert.Equal(4, scope.Native.Packet.Position);
        var team = new LegacyNativeMsgTeamArenaInfo { TodayRank = 1,
            Status = (LegacyNativeMsgTeamArenaInfo.Action)action, TotalWin = 2, TotalLose = 3,
            TodayWin = 4, TodayBattles = 5, HistoryHonor = 6, CurrentHonor = 7,
            ArenaPoints = uint.MaxValue, UnKnow2 = 8, UnKnow3 = 9 };
        scope.Native.Packet.TeamArenaInfoCreate(team);
        Assert.Equal(Words(2238, 1, action, 2, 3, 4, 5, 6, 7, uint.MaxValue, 8, 9),
            NativeSpellTestFixture.Bytes(scope.Native.Packet));
        Assert.Equal(4, scope.Native.Packet.Position); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task NullInfoCodecStillInitializesTheNativeWriterBeforeItsFirstFieldFault(bool team) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        if (team) Assert.Throws<NullReferenceException>(() => scope.Native.Packet.TeamArenaInfoCreate(null!));
        else Assert.Throws<NullReferenceException>(() => scope.Native.Packet.ArenaInfoCreate(null!));
        Assert.Equal(4, scope.Native.Packet.Position); Assert.Equal(321, scope.Native.Packet.Size);
        Assert.All(NativeSpellTestFixture.Bytes(scope.Native.Packet), value => Assert.Equal((byte)0xA5, value));
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task BothWholeHandlersUseTheirRealRecordAndKeepDistinctCallerStreamOwnership() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.ArenaStatistic = new LegacyNativeArenaUser();
        scope.Client.ArenaStatistic.Info.ArenaPoints = 41;
        CallHandler<LegacyNativeMsgArenaInfo>(scope.Client, scope.Native.Packet);
        Assert.Equal(Words(2191, 0, 0, 0, 0, 0, 0, 0, 0, 0, 41, 0, 0), scope.Queue.Dequeue());
        scope.Client.TeamArenaStatistic = new LegacyNativeTeamArenaUser();
        scope.Client.TeamArenaStatistic.Info.ArenaPoints = 42;
        CallHandler<LegacyNativeMsgTeamArenaInfo>(scope.Client, null!); // Original ignores caller stream.
        Assert.Equal(Words(2238, 0, 0, 0, 0, 0, 0, 0, 0, 42, 0, 0), scope.Queue.Dequeue());
        Assert.Equal(3, LegacyNativePacketRecycle.Count);
        Assert.Equal(0, scope.Borrowed[0].Position); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task MissingHandlerRecordFaultsBeforeWriterOrRecyclerMutation(bool team) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        if (team) Assert.Throws<NullReferenceException>(() => CallHandler<LegacyNativeMsgTeamArenaInfo>(scope.Client, scope.Native.Packet));
        else Assert.Throws<NullReferenceException>(() => CallHandler<LegacyNativeMsgArenaInfo>(scope.Client, scope.Native.Packet));
        Assert.Equal(237, scope.Native.Packet.Position); Assert.Equal(321, scope.Native.Packet.Size);
        Assert.Equal(4, LegacyNativePacketRecycle.Count); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(true, true)] [InlineData(false, false)] [InlineData(true, false)]
    public Task TeamInfoSendStillBuildsWholePacketWhenRealTransportSuppressesIt(bool fake, bool alive) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.Fake = fake; scope.Client.Socket!.Alive = alive;
        var info = new LegacyNativeMsgTeamArenaInfo { ArenaPoints = 77 };
        info.Send(scope.Client);
        Assert.Equal(Words(2238, 0, 0, 0, 0, 0, 0, 0, 0, 77, 0, 0),
            NativeSpellTestFixture.Bytes(scope.Borrowed[0]));
        Assert.Equal(0, scope.Borrowed[0].Position); Assert.Empty(scope.Queue);
        Assert.Equal(77u, info.ArenaPoints);
    });

    [Fact]
    public Task TeamInfoFactoryReturnsFreshZeroOwnersRatherThanAnSharedRecord() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var first = LegacyNativeMsgTeamArenaInfo.Create(); var second = LegacyNativeMsgTeamArenaInfo.Create();
        Assert.NotSame(first, second); first.ArenaPoints = 1;
        Assert.Equal(0u, second.ArenaPoints); Assert.Equal(LegacyNativeMsgTeamArenaInfo.Action.NotSignedUp, second.Status);
        Assert.Empty(scope.Queue);
    });
}
