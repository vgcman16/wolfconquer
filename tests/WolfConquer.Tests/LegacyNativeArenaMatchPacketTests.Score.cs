using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeArenaMatchPacketTests
{
    [Fact]
    public Task WholeScoreCodecWritesExactNinetySixBytesUsingTheConfiguredServerForBothPlayers() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var packet = scope.Packet;
        Assert.Same(packet, packet.ArenaMatchScoreCreate(0xABCDEF01, "First", 0xFEDCBA98, 7, "Second", uint.MaxValue));
        Assert.Equal(ScoreBytes(0xABCDEF01, "First", 0xFEDCBA98, 7, "Second", uint.MaxValue), NativeSpellTestFixture.Bytes(packet));
        Assert.Equal(96, packet.Size); Assert.Equal(4, packet.Position); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ScoreCodecRetainsNativeCharacterCountTruncationOfUtf8AndFixedAsciiLimit() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); string longName = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        scope.Packet.ArenaMatchScoreCreate(1, "狼X", 2, 3, longName, 4);
        byte[] expected = ScoreBytes(1, "", 2, 3, longName, 4); expected[12] = 0xE7; expected[13] = 0x8B;
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
    });

    [Fact]
    public Task ScoreCodecUsesRealSealWithoutIncludingItInTheNativeLengthWord() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); LegacyNativePacket.SealString = "TQServer";
        scope.Packet.ArenaMatchScoreCreate(1, "A", 2, 3, "B", 4);
        byte[] expected = ScoreBytes(1, "A", 2, 3, "B", 4).Concat(new byte[] { 84,81,83,101,114,118,101,114 }).ToArray();
        Assert.Equal(104, scope.Packet.Size); Assert.Equal(4, scope.Packet.Position);
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
    });

    [Fact]
    public Task ScoreCodecMissingServerRefusesWithoutOutputOrPlaceholderIdentity() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); LegacyNativeGroupServerList.MyServerInfo = null!;
        byte[] before = ArenaAllocation(scope.Packet);
        Assert.Throws<InvalidOperationException>(() => scope.Packet.ArenaMatchScoreCreate(0x1234, "A", 2, 3, "B", 4));
        Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Equal(before, ArenaAllocation(scope.Packet));
        Assert.Null(LegacyNativeGroupServerList.MyServerInfo); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ScoreCodecNullFirstNameRefusesBeforeAnyUidOrServerPublication() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        byte[] before = ArenaAllocation(scope.Packet);
        Assert.Throws<ArgumentNullException>(() => scope.Packet.ArenaMatchScoreCreate(1, null!, 2, 3, "B", 4));
        Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Equal(before, ArenaAllocation(scope.Packet));
    });

    [Fact]
    public Task WholeSendScoreUsesActualPlayerUidsAndRecordNamesDamageForAllPlayersAndWatchers() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var secondQueue = new Queue<byte[]>(); var watchingQueue = new Queue<byte[]>();
        var second = scope.Peer(18, secondQueue); var watching = scope.Peer(19, watchingQueue);
        var match = new LegacyNativeArenaMatchRuntime(scope.Client, second, 100);
        scope.Client.ArenaStatistic!.UID = 777; second.ArenaStatistic!.UID = 888;
        scope.Client.Player.ServerID = 2; second.Player.ServerID = 3; // Not the codec's server source.
        match.Watchers.TryAdd(123, watching);
        match.SendScore();
        byte[] expected = ScoreBytes(17, "RecordOne", 0x12345678, 18, "RecordTwo", 0x87654321);
        Assert.Equal(expected, Assert.Single(scope.Queue)); Assert.Equal(expected, Assert.Single(secondQueue));
        Assert.Equal(expected, Assert.Single(watchingQueue));
        Assert.Equal(0, scope.Borrowed.Position); Assert.Equal(96, scope.Borrowed.Size);
        Assert.Equal(0, LegacyNativePacketRecycle.Count); // Genuine Reuse rewinds, not enqueue.
        Assert.Equal(0x12345678u, scope.Client.ArenaStatistic.Damage);
        Assert.Equal(0x87654321u, second.ArenaStatistic.Damage);
    });

    [Fact]
    public Task SendScoreDoesNotDeduplicatePlayerWatcherAliasesOrNormalizeWatcherDictionaryKeys() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var secondQueue = new Queue<byte[]>();
        var second = scope.Peer(18, secondQueue); var match = new LegacyNativeArenaMatchRuntime(scope.Client, second, 100);
        match.Watchers.TryAdd(999, scope.Client); match.Watchers.TryAdd(1000, scope.Client);
        match.SendScore();
        Assert.Equal(3, scope.Queue.Count); Assert.Single(secondQueue);
        byte[] expected = ScoreBytes(17, "RecordOne", 0x12345678, 18, "RecordTwo", 0x87654321);
        Assert.All(scope.Queue, packet => Assert.Equal(expected, packet));
        Assert.Equal(2, match.Watchers.Count); Assert.False(match.Watchers.ContainsKey(17));
    });

    [Fact]
    public Task SendScoreBuildsBeforeSendingSoMissingRecordFaultsWithoutAnyRecipientPublication() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var secondQueue = new Queue<byte[]>();
        var second = scope.Peer(18, secondQueue); var match = new LegacyNativeArenaMatchRuntime(scope.Client, second, 100);
        second.ArenaStatistic = null;
        Assert.Throws<NullReferenceException>(match.SendScore);
        Assert.Empty(scope.Queue); Assert.Empty(secondQueue);
        Assert.Equal(0, scope.Borrowed.Position); Assert.Equal(321, scope.Borrowed.Size);
        Assert.Equal(0, LegacyNativePacketRecycle.Count);
    });

    [Fact]
    public Task SendScoreRetainsActualSendFilteringButContinuesToOtherRecipients() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var secondQueue = new Queue<byte[]>(); var watchQueue = new Queue<byte[]>();
        var second = scope.Peer(18, secondQueue); var watcher = scope.Peer(19, watchQueue);
        var match = new LegacyNativeArenaMatchRuntime(scope.Client, second, 100); match.Watchers.TryAdd(19, watcher);
        scope.Client.Fake = true; second.Socket!.Alive = false;
        match.SendScore(); Assert.Empty(scope.Queue); Assert.Empty(secondQueue);
        Assert.Equal(ScoreBytes(17, "RecordOne", 0x12345678, 18, "RecordTwo", 0x87654321), Assert.Single(watchQueue));
        Assert.Equal(0, scope.Borrowed.Position);
    });
}
