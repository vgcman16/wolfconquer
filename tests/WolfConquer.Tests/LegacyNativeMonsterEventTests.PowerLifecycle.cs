using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterEventTests
{
    [Fact]
    public Task IdleCountdownPublishesThenResetsPrepareTimeAndDecrements() => NativePacketTestThread.Run(() =>
    {
        using var state = new State(); using var packets = new Packets(); packets.Publish();
        var p = new LegacyNativeMsgPowerArena { Process = LegacyNativeProcesType.Idle, StartMinutes = 4, PrepareStart = DateTime.Now.AddMinutes(-2) };
        DateTime before = DateTime.Now; p.CheckUp(); DateTime after = DateTime.Now;
        Assert.Equal("Power Arena will be opened in 4 minutes. Please get ready for that!", Text(Assert.Single(packets.Queue)));
        Assert.Equal(3, p.StartMinutes); Assert.Equal(LegacyNativeProcesType.Idle, p.Process); Assert.InRange(p.PrepareStart, before, after); Assert.Equal(default, p.StartTimer);
    });
    [Fact]
    public Task ZeroCountdownOpensFullAliveStateAndBeginsNativeTimer() => NativePacketTestThread.Run(() =>
    {
        using var state = new State(); using var packets = new Packets(); packets.Publish();
        var p = new LegacyNativeMsgPowerArena { Process = LegacyNativeProcesType.Idle, StartMinutes = 0, PrepareStart = DateTime.Now.AddMinutes(-2) };
        DateTime before = DateTime.Now; p.CheckUp(); DateTime after = DateTime.Now;
        Assert.Equal("The Power Arena is open! Find Arena Manager Wang in Twin City (465,234) to sign up for the Arena.", Text(Assert.Single(packets.Queue)));
        Assert.Equal(LegacyNativeProcesType.Alive, p.Process); Assert.Equal(5, p.StartMinutes); Assert.InRange(p.StartTimer, before, after);
    });
    [Fact]
    public Task AliveClosingWarningUsesCurrentMinutesAndUpdatesOnlyWarningState() => NativePacketTestThread.Run(() =>
    {
        using var state = new State(); using var packets = new Packets(); packets.Publish();
        var start = DateTime.Now.AddMinutes(-56); var p = new LegacyNativeMsgPowerArena { Process = LegacyNativeProcesType.Alive, StartTimer = start, PrepareStart = DateTime.Now.AddMinutes(-2), StartMinutes = 1 };
        DateTime before = DateTime.Now; p.CheckUp(); DateTime after = DateTime.Now;
        Assert.Equal("Power Arena will be closed in 1 minutes. Go and claim your reward now!", Text(Assert.Single(packets.Queue)));
        Assert.Equal(0, p.StartMinutes); Assert.Equal(start, p.StartTimer); Assert.InRange(p.PrepareStart, before, after); Assert.Equal(LegacyNativeProcesType.Alive, p.Process);
        p.CheckUp(); Assert.Single(packets.Queue);
    });
    [Fact]
    public Task FinishThenClosingWarningBothOccurInTheSameExpiredAliveCall() => NativePacketTestThread.Run(() =>
    {
        using var state = new State(); using var packets = new Packets(); packets.Publish();
        var p = new LegacyNativeMsgPowerArena { Process = LegacyNativeProcesType.Alive, StartTimer = DateTime.Now.AddMinutes(-61), PrepareStart = DateTime.Now.AddMinutes(-2), StartMinutes = 5 };
        p.CheckUp(); Assert.Equal(LegacyNativeProcesType.Dead, p.Process); Assert.Equal(4, p.StartMinutes);
        Assert.Equal(new[] { "The Power Arena is closed, please come tomorrow!", "Power Arena will be closed in 5 minutes. Go and claim your reward now!" }, packets.Queue.Select(Text));
        p.CheckUp(); Assert.Equal(2, packets.Queue.Count);
    });
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task BroadcastFailureKeepsStateBeforeTheOriginalPostSendWrites(bool opening) => NativePacketTestThread.Run(() =>
    {
        using var state = new State(); using var packets = new Packets(); var old = DateTime.Now.AddMinutes(-2);
        var p = new LegacyNativeMsgPowerArena { Process = LegacyNativeProcesType.Idle, StartMinutes = opening ? 0 : 4, PrepareStart = old };
        packets.FailNextTake(); Assert.Throws<NullReferenceException>(p.CheckUp);
        Assert.Equal(LegacyNativeProcesType.Idle, p.Process); Assert.Equal(opening ? 0 : 4, p.StartMinutes); Assert.Equal(old, p.PrepareStart); Assert.Equal(default, p.StartTimer);
    });
    [Fact]
    public Task FinishPublicationFailureDoesNotMarkTheEventDead() => NativePacketTestThread.Run(() =>
    {
        using var state = new State(); using var packets = new Packets(); var p = new LegacyNativeMsgPowerArena { Process = LegacyNativeProcesType.Alive };
        packets.FailNextTake(); Assert.Throws<NullReferenceException>(p.Finish); Assert.Equal(LegacyNativeProcesType.Alive, p.Process);
    });
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task DateRangeFaultsRemainBeforeAnyAnnouncement(bool idle) => NativePacketTestThread.Run(() =>
    {
        using var state = new State(); using var packets = new Packets(); packets.Publish();
        var p = new LegacyNativeMsgPowerArena { Process = idle ? LegacyNativeProcesType.Idle : LegacyNativeProcesType.Alive, PrepareStart = DateTime.MaxValue, StartTimer = DateTime.MaxValue };
        Assert.Throws<ArgumentOutOfRangeException>(p.CheckUp); Assert.Empty(packets.Queue); Assert.Equal(DateTime.MaxValue, p.StartTimer); Assert.Equal(DateTime.MaxValue, p.PrepareStart);
    });
    [Fact]
    public Task FinishBroadcastsToEveryCurrentClientWithoutAnArenaMapFilter() => NativePacketTestThread.Run(() =>
    {
        using var state = new State(); using var packets = new Packets(); packets.Publish();
        var peerQueue = new Queue<byte[]>(); var peer = NativeSpellTestFixture.CreateOwner(peerQueue); peer.Player.UID = 18; peer.Player.Map = 0;
        LegacyNativePoolClients.GamePoll![18] = peer;
        var p = new LegacyNativeMsgPowerArena { Process = LegacyNativeProcesType.Alive }; p.Finish();
        Assert.Equal(Assert.Single(packets.Queue), Assert.Single(peerQueue)); Assert.Equal(LegacyNativeProcesType.Dead, p.Process);
    });
}
