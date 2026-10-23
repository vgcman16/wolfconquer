using System.Text;
using WolfConquer.Core.World;
using Table = WolfConquer.Core.Data.LegacyNativeRoulettes.RouletteTable;
using Color = WolfConquer.Core.World.LegacyNativeMsgRouletteOpenGui.Color;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeViewTableDependencyTests
{
    [Fact]
    public Task WholeJoinAndWatchPublishRealFramesAndSecondPlayerSharesExistingBets() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var table = s.Table();
        table.AddPlayer(s.Packet, s.Client);
        Assert.Equal(700u, s.Client.PlayRouletteUID);
        Assert.Same(s.Client, table.RegistredPlayers[17].Owner);
        Assert.Equal(Color.Blue, table.RegistredPlayers[17].Color);
        byte[] firstGui = Assert.Single(s.Queue);
        Assert.Equal((ushort)2030, Op(firstGui)); Assert.Equal((byte)0, firstGui[10]);
        s.Queue.Clear();

        var watcherQueue = new Queue<byte[]>(); var watcher = s.Peer(31, watcherQueue);
        table.AddWatch(s.Packet, watcher);
        Assert.Equal(700u, watcher.WatchRoulette); Assert.Same(watcher, table.ClientsWatch[31]);
        Assert.Equal(new ushort[] { 2112 }, s.Queue.Select(Op));
        Assert.Equal(new ushort[] { 2112, 2030 }, watcherQueue.Select(Op));
        var watchGui = watcherQueue.Last();
        Assert.Equal((byte)99, watchGui[8]); Assert.Equal((byte)1, watchGui[10]);
        Assert.Equal(17u, BitConverter.ToUInt32(watchGui, 11));
        table.RegistredPlayers[17].MyLuckNumber[5] = new() { Number = 5, BetPrice = 20 };
        s.Queue.Clear(); watcherQueue.Clear();

        var peerQueue = new Queue<byte[]>(); var peer = s.Peer(32, peerQueue);
        table.AddPlayer(s.Packet, peer);
        Assert.Equal(Color.Yellow, table.RegistredPlayers[32].Color);
        Assert.Equal(700u, peer.PlayRouletteUID);
        Assert.Equal(new ushort[] { 2030, 2090 }, peerQueue.Select(Op));
        Assert.Equal(new ushort[] { 2019 }, s.Queue.Select(Op));
        Assert.Equal(new ushort[] { 2019 }, watcherQueue.Select(Op));
        Assert.Equal(32u, BitConverter.ToUInt32(s.Queue.Peek(), 4));
        Assert.Equal((byte)1, s.Queue.Peek()[12]);
    });

    [Fact]
    public Task RemoveMissingPlayerStillBroadcastsBeforeFailedRemovalAndPreservesSlots() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var table = s.Table(); table.AddPlayer(s.Packet, s.Client); s.Queue.Clear();
        var absent = s.Peer(99, new Queue<byte[]>()); absent.PlayRouletteUID = 123;
        table.RemovePlayer(absent);
        byte[] frame = Assert.Single(s.Queue);
        Assert.Equal((ushort)2048, Op(frame)); Assert.Equal((byte)7, frame[4]);
        Assert.Equal(99u, BitConverter.ToUInt32(frame, 5));
        Assert.Equal(123u, absent.PlayRouletteUID); Assert.Single(table.RegistredPlayers);
    });

    [Fact]
    public Task LastPlayerRemovalAlsoRemovesWatchersAfterRealTableQuitBroadcast() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var table = s.Table(); table.AddPlayer(s.Packet, s.Client);
        var peerQueue = new Queue<byte[]>(); var peer = s.Peer(18, peerQueue); table.AddPlayer(s.Packet, peer);
        var watchQueue = new Queue<byte[]>(); var watcher = s.Peer(19, watchQueue); table.AddWatch(s.Packet, watcher);
        s.Queue.Clear(); peerQueue.Clear(); watchQueue.Clear();
        table.RemovePlayer(s.Client);
        Assert.Equal(0u, s.Client.PlayRouletteUID); Assert.Equal(700u, peer.PlayRouletteUID);
        Assert.Equal(700u, watcher.WatchRoulette); Assert.Single(table.RegistredPlayers); Assert.Single(table.ClientsWatch);
        Assert.Single(s.Queue); Assert.Single(peerQueue); Assert.Single(watchQueue);
        table.RemovePlayer(peer);
        Assert.Equal(0u, peer.PlayRouletteUID); Assert.Equal(0u, watcher.WatchRoulette);
        Assert.Empty(table.RegistredPlayers); Assert.Empty(table.ClientsWatch);
        Assert.Equal(new ushort[] { 2048, 2048, 2048 }, watchQueue.Select(Op));
        Assert.Equal(19u, BitConverter.ToUInt32(watchQueue.Last(), 5));
        table.RemoveWatch(19); Assert.Equal(3, watchQueue.Count);
    });

    [Theory]
    [InlineData(0, "No one is at the table.")]
    [InlineData(1, "You are play another table.")]
    [InlineData(2, "You are spectating another table.")]
    public Task WholeWatchAdmissionUsesSourceGuardOrderWithoutMembershipMutation(int guard, string message) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var table = s.Table();
        if (guard != 0) table.RegistredPlayers[18] = s.Member(s.Peer(18, new Queue<byte[]>()));
        if (guard == 1) { s.Client.PlayRouletteUID = 99; s.Client.WatchRoulette = 88; }
        if (guard == 2) s.Client.WatchRoulette = 88;
        table.AddWatch(s.Packet, s.Client);
        byte[] warning = Assert.Single(s.Queue);
        Assert.Equal((ushort)2179, Op(warning));
        Assert.Contains(message, Encoding.UTF8.GetString(warning));
        Assert.Empty(table.ClientsWatch);
        Assert.Equal(guard == 0 ? 0u : guard == 1 ? 99u : 0u, s.Client.PlayRouletteUID);
    });

    [Fact]
    public Task WholeJoinCapacityPrecedesOtherAdmissionAndDuplicateDoesNotRepublish() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var table = s.Table();
        for (uint i = 1; i <= 5; i++) table.RegistredPlayers[i] = s.Member(s.Peer(i, new Queue<byte[]>()));
        s.Client.PlayRouletteUID = 55;
        table.AddPlayer(s.Packet, s.Client);
        Assert.Contains("Sorry, this game table is full.", Encoding.UTF8.GetString(Assert.Single(s.Queue)));
        Assert.Equal(55u, s.Client.PlayRouletteUID); Assert.Equal(5, table.RegistredPlayers.Count);
        table.RegistredPlayers.Clear(); s.Queue.Clear();
        table.AddPlayer(s.Packet, s.Client);
        Assert.Contains("You are play another table.", Encoding.UTF8.GetString(Assert.Single(s.Queue)));
        s.Client.PlayRouletteUID = 0; s.Queue.Clear(); table.RegistredPlayers[17] = s.Member();
        table.AddPlayer(s.Packet, s.Client);
        Assert.Equal(0u, s.Client.PlayRouletteUID); Assert.Empty(s.Queue); Assert.Single(table.RegistredPlayers);
    });

    [Fact]
    public Task JoinFaultCommitsSlotMemberAndColorBeforeApplyTimeTouchesNullStream() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var table = s.Table();
        Assert.Throws<NullReferenceException>(() => table.AddPlayer(null!, s.Client));
        Assert.Equal(700u, s.Client.PlayRouletteUID);
        var member = Assert.Single(table.RegistredPlayers).Value;
        Assert.Same(s.Client, member.Owner); Assert.Equal(Color.Blue, member.Color); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task WatchFaultCommitsSlotAndMembershipBeforeScreenPacketConstruction() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var table = s.Table(); table.RegistredPlayers[18] = s.Member(s.Peer(18, new Queue<byte[]>()));
        Assert.Throws<NullReferenceException>(() => table.AddWatch(null!, s.Client));
        Assert.Equal(700u, s.Client.WatchRoulette);
        Assert.Same(s.Client, table.ClientsWatch[17]); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task MemberShareBroadcastAndWinnerNotificationUseActualPacketRecyclerAndSenders() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var table = s.Table();
        var member = s.Member(); table.RegistredPlayers[17] = member;
        var watchQueue = new Queue<byte[]>(); table.ClientsWatch[9] = s.Peer(9, watchQueue);
        member.MyLuckNumber[7] = new() { Number = 7, BetPrice = 99 };
        member.ShareBetting(table);
        Assert.Equal((ushort)2090, Op(Assert.Single(s.Queue)));
        Assert.Equal(s.Queue.Peek(), Assert.Single(watchQueue));
        table.LuckyNumber = 37; table.ApplayNumberWinner(s.Client);
        Assert.Equal(Frame(2330, writer => writer.Write((byte)37)), s.Queue.Last());
        member.MyLuckNumber.Clear(); s.Queue.Clear();
        table.ShareTableBetting(s.Packet, s.Client); Assert.Empty(s.Queue);
    });
}
