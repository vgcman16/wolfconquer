using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterScoreTests
{
    [Fact]
    public Task ForeignNullHunterFaultsDuringRankingAfterPublishingTheNewKillerRecord() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); using var logs = new Logs();
        s.Monster.Hunters[20] = null!;
        s.Monster.UpdateScore(s.Packet, s.Client, 7);
        Assert.Null(s.Monster.Hunters[20]);
        Assert.Equal(7u, s.Monster.Hunters[17].HunterScore);
        Assert.Equal(0u, s.Monster.Hunters[17].Rank);
        Assert.Empty(s.Queue); logs.AssertTwoRealLogs();
    });

    [Fact]
    public Task MissingFamilyFaultsAfterRankMutationButBeforeThePacketWriterResetsTheStream() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); using var logs = new Logs();
        s.Monster.Family = null!;
        s.Packet.Seek(37);
        s.Monster.UpdateScore(s.Packet, s.Client, 7);
        Assert.Equal(7u, s.Monster.Hunters[17].HunterScore);
        Assert.Equal(1u, s.Monster.Hunters[17].Rank);
        Assert.Equal(37, s.Packet.Position);
        Assert.Empty(s.Queue); logs.AssertTwoRealLogs();
    });

    [Fact]
    public Task NullStreamIsCaughtAfterRealScoreAndRankMutationThenLogsTwice() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); using var logs = new Logs();
        s.Monster.UpdateScore(null!, s.Client, 7);
        var row = Assert.Single(s.Monster.Hunters).Value;
        Assert.Equal(7u, row.HunterScore); Assert.Equal(1u, row.Rank); Assert.Empty(s.Queue);
        logs.AssertTwoRealLogs();
    });

    [Fact]
    public Task NullHuntersIsRebornBeforeMissingKillerFaultAndTheWholeCatchStillLogsTwice() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); using var logs = new Logs();
        s.Monster.Hunters = null!;
        s.Monster.UpdateScore(s.Packet, null!, 7);
        Assert.NotNull(s.Monster.Hunters); Assert.Empty(s.Monster.Hunters); Assert.Empty(s.Queue);
        logs.AssertTwoRealLogs();
    });

    [Fact]
    public Task NullExistingHunterFaultIsNotReplacedOrSkippedAndNoRankPacketIsSent() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); using var logs = new Logs();
        s.Monster.Hunters[17] = null!;
        s.Monster.UpdateScore(s.Packet, s.Client, 7);
        Assert.Null(s.Monster.Hunters[17]); Assert.Empty(s.Queue); logs.AssertTwoRealLogs();
    });

    [Fact]
    public Task MissingMonsterMapFaultOccursAfterRankingAndPacketConstructionBeforeBroadcast() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); using var logs = new Logs();
        s.Monster.GMap = null;
        s.Monster.UpdateScore(s.Packet, s.Client, 7);
        Assert.Equal(7u, s.Monster.Hunters[17].HunterScore);
        Assert.Equal(1u, s.Monster.Hunters[17].Rank);
        Assert.Equal(Ranking(2, 3130, Hunter(17, 7, "Wolf", 1, 2)), NativeSpellTestFixture.Bytes(s.Packet));
        Assert.Empty(s.Queue); logs.AssertTwoRealLogs();
    });
}
